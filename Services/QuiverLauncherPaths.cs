namespace QuiverLauncher.Services;

/// <summary>
/// Resolves the user-data root for Quiver Launcher.
/// Windows Velopack: <c>RootAppDir</c> (sibling of <c>current/</c>).
/// Linux/macOS Velopack: writable directory beside the AppImage / .app when possible;
/// otherwise XDG / Application Support.
/// </summary>
public static class QuiverLauncherPaths
{
    public const string AppName = "Fletch";

    /// <summary>Test override for the user-data root.</summary>
    public static string? OverrideUserDataRoot { get; set; }

    /// <summary>
    /// Optional provider for Velopack's <c>RootAppDir</c>. Set from startup after
    /// <c>VelopackApp.Build().Run()</c>. Used on Windows.
    /// </summary>
    public static Func<string?>? VelopackRootAppDirProvider { get; set; }

    /// <summary>
    /// Optional provider for the directory containing the Velopack package
    /// (folder with the <c>.AppImage</c>, or parent of the <c>.app</c> bundle).
    /// Used on Linux/macOS for portable sidecar library data.
    /// </summary>
    public static Func<string?>? VelopackPackageDirectoryProvider { get; set; }

    /// <summary>
    /// Optional provider for Android <c>Context.FilesDir</c>. Set from MainActivity.
    /// </summary>
    public static Func<string?>? AndroidFilesDirProvider { get; set; }

    /// <summary>Test hook to override directory writability checks.</summary>
    internal static Func<string, bool>? DirectoryWritableTester { get; set; }

    public static string UserDataRoot
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(OverrideUserDataRoot))
                return NormalizeDirectory(OverrideUserDataRoot);

            if (OperatingSystem.IsAndroid())
            {
                var androidRoot = AndroidFilesDirProvider?.Invoke();
                if (!string.IsNullOrWhiteSpace(androidRoot))
                    return NormalizeDirectory(androidRoot);
            }

            if (OperatingSystem.IsWindows())
            {
                var velopackRoot = VelopackRootAppDirProvider?.Invoke();
                if (!string.IsNullOrWhiteSpace(velopackRoot))
                    return NormalizeDirectory(velopackRoot);

                // Unpackaged / debug: keep data beside the build output.
                return NormalizeDirectory(AppDomain.CurrentDomain.BaseDirectory);
            }

            return ResolveUnixUserDataRoot(
                VelopackPackageDirectoryProvider?.Invoke(),
                GetOsFallbackUserDataRoot());
        }
    }

    /// <summary>
    /// Prefer a writable Velopack package directory (beside AppImage / .app); otherwise OS fallback.
    /// A library already kept in the OS folder (~/.local/share/QuiverLauncher) wins over an empty
    /// package directory, so a player can keep their data there (#133).
    /// </summary>
    internal static string ResolveUnixUserDataRoot(string? packageDirectory, string fallbackRoot)
    {
        if (!string.IsNullOrWhiteSpace(packageDirectory) && ReadMoveMarker(packageDirectory) == MoveCopied &&
            HasLibraryData(fallbackRoot))
            return NormalizeDirectory(fallbackRoot);

        if (!string.IsNullOrWhiteSpace(packageDirectory) && IsDirectoryWritable(packageDirectory) &&
            (HasLibraryData(packageDirectory) || !HasLibraryData(fallbackRoot)))
            return NormalizeDirectory(packageDirectory);

        return NormalizeDirectory(fallbackRoot);
    }

    /// <summary>Whether <paramref name="directory"/> holds a Quiver library (settings or app list).</summary>
    internal static bool HasLibraryData(string? directory) =>
        !string.IsNullOrWhiteSpace(directory) &&
        (File.Exists(Path.Combine(directory, "settings.json")) || File.Exists(Path.Combine(directory, "apps.json")));

    /// <summary>
    /// Files and folders Quiver keeps in its data folder. Only these move: the folder beside an
    /// AppImage often holds other AppImages too. Installed apps (Apps) stay where they are.
    /// </summary>
    internal static readonly string[] MovableDataEntries =
        ["settings.json", "apps.json", "games.json", "Cache", "Backups", "crash.log",
            LaunchDebugReport.UserDataFileName, GamepadDebugLog.FileName];

    internal const string MoveMarkerFileName = "move-data-to-user-folder";
    private const string MoveRequested = "requested";
    private const string MoveCopied = "copied";
    private const string MoveStagingName = ".moving-in";
    private const string MoveStagingCompleteName = ".complete";

    /// <summary>The OS folder the data moves to: ~/.local/share/QuiverLauncher, or Application Support on macOS.</summary>
    public static string UserFolderDataRoot => NormalizeDirectory(GetOsFallbackUserDataRoot());

    /// <summary>Data is kept beside the AppImage / .app, so it can be moved to <see cref="UserFolderDataRoot"/>.</summary>
    public static bool CanMoveDataToUserFolder
    {
        get
        {
            if (OperatingSystem.IsWindows() || OperatingSystem.IsAndroid() || !string.IsNullOrWhiteSpace(OverrideUserDataRoot))
                return false;
            var package = VelopackPackageDirectoryProvider?.Invoke();
            return !string.IsNullOrWhiteSpace(package) &&
                string.Equals(NormalizeDirectory(package), UserDataRoot, StringComparison.Ordinal) &&
                !HasLibraryData(UserFolderDataRoot);
        }
    }

    /// <summary>Asks for the data to move to <see cref="UserFolderDataRoot"/> the next time Quiver starts.</summary>
    public static void RequestMoveToUserFolder() =>
        File.WriteAllText(Path.Combine(UserDataRoot, MoveMarkerFileName), MoveRequested);

    /// <summary>Finishes a move asked for with <see cref="RequestMoveToUserFolder"/>. Call before anything reads the data.</summary>
    public static void CompletePendingDataMove()
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsAndroid() || !string.IsNullOrWhiteSpace(OverrideUserDataRoot))
            return;
        var package = VelopackPackageDirectoryProvider?.Invoke();
        if (string.IsNullOrWhiteSpace(package)) return;
        try { CompletePendingDataMove(package, GetOsFallbackUserDataRoot()); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Moving Quiver's data failed: {ex.Message}"); }
    }

    /// <summary>
    /// Copies everything into a staging folder first and only then into place, and deletes the
    /// originals last, so a move cut short by a crash or power loss picks up where it stopped.
    /// Until the copy is complete the originals stay in use.
    /// </summary>
    internal static void CompletePendingDataMove(string packageDirectory, string targetDirectory)
    {
        var markerPath = Path.Combine(packageDirectory, MoveMarkerFileName);
        var state = ReadMoveMarker(packageDirectory);
        if (state == null) return;
        var staging = Path.Combine(targetDirectory, MoveStagingName);

        if (state == MoveRequested)
        {
            var stagingComplete = File.Exists(Path.Combine(staging, MoveStagingCompleteName));
            // A library that was already there isn't Quiver's to replace: keep using the package directory.
            if (HasLibraryData(targetDirectory) && !Directory.Exists(staging))
            {
                File.Delete(markerPath);
                return;
            }

            if (!stagingComplete)
            {
                if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
                Directory.CreateDirectory(staging);
                foreach (var name in MovableDataEntries)
                    CopyEntry(Path.Combine(packageDirectory, name), Path.Combine(staging, name));
                File.WriteAllText(Path.Combine(staging, MoveStagingCompleteName), "");
            }

            // Same file system as the target, so each of these is a rename.
            foreach (var name in MovableDataEntries)
            {
                var staged = Path.Combine(staging, name);
                var destination = Path.Combine(targetDirectory, name);
                if (File.Exists(staged)) File.Move(staged, destination, overwrite: true);
                else if (Directory.Exists(staged))
                {
                    if (Directory.Exists(destination)) Directory.Delete(destination, recursive: true);
                    Directory.Move(staged, destination);
                }
            }
            File.WriteAllText(markerPath, MoveCopied);
        }

        if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        foreach (var name in MovableDataEntries)
        {
            var original = Path.Combine(packageDirectory, name);
            if (File.Exists(original)) File.Delete(original);
            else if (Directory.Exists(original)) Directory.Delete(original, recursive: true);
        }
        File.Delete(markerPath);
    }

    private static string? ReadMoveMarker(string directory)
    {
        try
        {
            var path = Path.Combine(directory, MoveMarkerFileName);
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static void CopyEntry(string source, string destination)
    {
        if (File.Exists(source))
        {
            File.Copy(source, destination, overwrite: true);
            return;
        }
        if (!Directory.Exists(source)) return;
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        foreach (var directory in Directory.EnumerateDirectories(source))
            CopyEntry(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    public static string AppsJsonPath => Path.Combine(UserDataRoot, "apps.json");
    public static string SettingsJsonPath => Path.Combine(UserDataRoot, "settings.json");
    public static string GamesJsonPath => Path.Combine(UserDataRoot, "games.json");
    public static string LegacyGamesJsonPath => Path.Combine(UserDataRoot, "games.json");
    public static string CacheDirectory => Path.Combine(UserDataRoot, "Cache");
    public static string DefaultAppsDirectory => Path.Combine(UserDataRoot, "Apps");
    public static string CrashLogPath => Path.Combine(UserDataRoot, "crash.log");
    public static string GamepadDebugLogPath => Path.Combine(UserDataRoot, GamepadDebugLog.FileName);

    public static void EnsureUserDataRootExists()
    {
        Directory.CreateDirectory(UserDataRoot);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(DefaultAppsDirectory);
    }

    /// <summary>
    /// Resolves the portable library folder for a macOS Velopack/.app install:
    /// parent of the <c>.app</c> bundle when detectable.
    /// </summary>
    public static string? ResolveMacOsPackageDirectory(
        string? rootAppDir,
        string? appContentDir = null,
        string? fallbackStartDir = null)
    {
        var fromRoot = ParentIfAppBundle(rootAppDir);
        if (!string.IsNullOrWhiteSpace(fromRoot))
            return fromRoot;

        var fromContent = FindAppBundleParent(appContentDir);
        if (!string.IsNullOrWhiteSpace(fromContent))
            return fromContent;

        return FindAppBundleParent(fallbackStartDir);
    }

    internal static bool IsDirectoryWritable(string directory)
    {
        if (DirectoryWritableTester != null)
            return DirectoryWritableTester(directory);

        try
        {
            if (string.IsNullOrWhiteSpace(directory))
                return false;

            var full = NormalizeDirectory(directory);
            if (!Directory.Exists(full))
                Directory.CreateDirectory(full);

            var probe = Path.Combine(full, ".quiver_write_probe_" + Guid.NewGuid().ToString("N"));
            using (File.Create(probe, bufferSize: 1, FileOptions.DeleteOnClose))
            {
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static string GetOsFallbackUserDataRoot()
    {
        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Application Support",
                AppName);
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(xdg))
            return Path.Combine(xdg, AppName);

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local",
            "share",
            AppName);
    }

    private static string? ParentIfAppBundle(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var normalized = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (normalized.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            return Path.GetDirectoryName(normalized);

        return null;
    }

    private static string? FindAppBundleParent(string? startDirectory)
    {
        if (string.IsNullOrWhiteSpace(startDirectory))
            return null;

        try
        {
            var current = new DirectoryInfo(NormalizeDirectory(startDirectory));
            while (current != null)
            {
                if (current.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                    return current.Parent?.FullName;

                current = current.Parent;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    internal static string NormalizeDirectory(string path)
    {
        var full = Path.GetFullPath(path);
        // The top of a drive keeps its separator: on Windows "G:" alone means the current folder on G:,
        // which for Quiver is current\, the folder every update replaces.
        return string.Equals(full, Path.GetPathRoot(full), StringComparison.Ordinal)
            ? full
            : full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
