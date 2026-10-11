using FluentAssertions;
using QuiverLauncher.Services;

namespace QuiverLauncher.Tests;

public class QuiverLauncherPathsTests : IDisposable
{
    private readonly string? _previousOverride;
    private readonly Func<string?>? _previousPackageDirProvider;
    private readonly Func<string, bool>? _previousWritableTester;

    public QuiverLauncherPathsTests()
    {
        _previousOverride = QuiverLauncherPaths.OverrideUserDataRoot;
        _previousPackageDirProvider = QuiverLauncherPaths.VelopackPackageDirectoryProvider;
        _previousWritableTester = QuiverLauncherPaths.DirectoryWritableTester;
    }

    public void Dispose()
    {
        QuiverLauncherPaths.OverrideUserDataRoot = _previousOverride;
        QuiverLauncherPaths.VelopackPackageDirectoryProvider = _previousPackageDirProvider;
        QuiverLauncherPaths.DirectoryWritableTester = _previousWritableTester;
    }

    [Fact]
    public void UserDataRoot_respects_override()
    {
        var temp = Path.Combine(Path.GetTempPath(), "QuiverLauncherPaths_" + Guid.NewGuid().ToString("N"));
        QuiverLauncherPaths.OverrideUserDataRoot = temp;

        QuiverLauncherPaths.UserDataRoot.Should().Be(Path.GetFullPath(temp));
        QuiverLauncherPaths.AppsJsonPath.Should().Be(Path.Combine(Path.GetFullPath(temp), "apps.json"));
        QuiverLauncherPaths.SettingsJsonPath.Should().Be(Path.Combine(Path.GetFullPath(temp), "settings.json"));
        QuiverLauncherPaths.DefaultAppsDirectory.Should().Be(Path.Combine(Path.GetFullPath(temp), "Apps"));
        QuiverLauncherPaths.CacheDirectory.Should().Be(Path.Combine(Path.GetFullPath(temp), "Cache"));
    }

    [Fact]
    public void ResolveMacOsPackageDirectory_returns_parent_of_app_bundle()
    {
        var portableRoot = Path.Combine(Path.GetTempPath(), "QuiverMacPortable_" + Guid.NewGuid().ToString("N"));
        var appBundle = Path.Combine(portableRoot, "Quiver.app");
        var contents = Path.Combine(appBundle, "Contents", "MacOS");
        Directory.CreateDirectory(contents);
        try
        {
            QuiverLauncherPaths.ResolveMacOsPackageDirectory(appBundle)
                .Should().Be(Path.GetFullPath(portableRoot));

            QuiverLauncherPaths.ResolveMacOsPackageDirectory(
                    rootAppDir: null,
                    appContentDir: contents)
                .Should().Be(Path.GetFullPath(portableRoot));
        }
        finally
        {
            Directory.Delete(portableRoot, recursive: true);
        }
    }

    [Fact]
    public void ResolveUnixUserDataRoot_prefers_writable_package_directory()
    {
        var packageDir = Path.Combine(Path.GetTempPath(), "QuiverPkg_" + Guid.NewGuid().ToString("N"));
        var fallback = Path.Combine(Path.GetTempPath(), "QuiverFallback_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(packageDir);
        try
        {
            QuiverLauncherPaths.DirectoryWritableTester = dir =>
                string.Equals(Path.GetFullPath(dir), Path.GetFullPath(packageDir), StringComparison.OrdinalIgnoreCase);

            QuiverLauncherPaths.ResolveUnixUserDataRoot(packageDir, fallback)
                .Should().Be(Path.GetFullPath(packageDir));
        }
        finally
        {
            Directory.Delete(packageDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveUnixUserDataRoot_falls_back_when_package_directory_not_writable()
    {
        var packageDir = Path.Combine(Path.GetTempPath(), "QuiverPkgRo_" + Guid.NewGuid().ToString("N"));
        var fallback = Path.Combine(Path.GetTempPath(), "QuiverFallback_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fallback);
        try
        {
            QuiverLauncherPaths.DirectoryWritableTester = _ => false;

            QuiverLauncherPaths.ResolveUnixUserDataRoot(packageDir, fallback)
                .Should().Be(Path.GetFullPath(fallback));
        }
        finally
        {
            Directory.Delete(fallback, recursive: true);
        }
    }

    [Fact]
    public void IsDirectoryWritable_detects_writable_temp_folder()
    {
        var temp = Path.Combine(Path.GetTempPath(), "QuiverWritable_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            QuiverLauncherPaths.DirectoryWritableTester = null;
            QuiverLauncherPaths.IsDirectoryWritable(temp).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    private static string TempDir(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), name + "_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Write(string path, string text = "{}")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    [Fact]
    public void ResolveUnixUserDataRoot_uses_a_library_already_in_the_user_folder()
    {
        var packageDir = TempDir("QuiverPkg");
        var fallback = TempDir("QuiverFallback");
        try
        {
            QuiverLauncherPaths.DirectoryWritableTester = null;
            Write(Path.Combine(fallback, "settings.json"));

            QuiverLauncherPaths.ResolveUnixUserDataRoot(packageDir, fallback).Should().Be(Path.GetFullPath(fallback));

            Write(Path.Combine(packageDir, "apps.json"));
            QuiverLauncherPaths.ResolveUnixUserDataRoot(packageDir, fallback)
                .Should().Be(Path.GetFullPath(packageDir), "a library beside the AppImage keeps being used");
        }
        finally
        {
            Directory.Delete(packageDir, recursive: true);
            Directory.Delete(fallback, recursive: true);
        }
    }

    [Fact]
    public void CompletePendingDataMove_moves_only_quivers_files_and_leaves_installed_apps()
    {
        var packageDir = TempDir("QuiverPkg");
        var target = Path.Combine(TempDir("QuiverHome"), "QuiverLauncher");
        try
        {
            QuiverLauncherPaths.DirectoryWritableTester = null;
            Write(Path.Combine(packageDir, "settings.json"), "settings");
            Write(Path.Combine(packageDir, "apps.json"), "apps");
            Write(Path.Combine(packageDir, "Cache", "CustomIcons", "game_custom.png"), "icon");
            Write(Path.Combine(packageDir, "Apps", "game", "game.x86_64"), "game");
            Write(Path.Combine(packageDir, "Other.AppImage"), "other");
            Write(Path.Combine(packageDir, QuiverLauncherPaths.MoveMarkerFileName), "requested");

            QuiverLauncherPaths.CompletePendingDataMove(packageDir, target);

            File.ReadAllText(Path.Combine(target, "settings.json")).Should().Be("settings");
            File.ReadAllText(Path.Combine(target, "apps.json")).Should().Be("apps");
            File.ReadAllText(Path.Combine(target, "Cache", "CustomIcons", "game_custom.png")).Should().Be("icon");
            Directory.Exists(Path.Combine(target, "Apps")).Should().BeFalse();
            Directory.Exists(Path.Combine(target, ".moving-in")).Should().BeFalse();
            File.Exists(Path.Combine(packageDir, "settings.json")).Should().BeFalse();
            File.Exists(Path.Combine(packageDir, "apps.json")).Should().BeFalse();
            Directory.Exists(Path.Combine(packageDir, "Cache")).Should().BeFalse();
            File.Exists(Path.Combine(packageDir, "Apps", "game", "game.x86_64")).Should().BeTrue();
            File.Exists(Path.Combine(packageDir, "Other.AppImage")).Should().BeTrue();
            File.Exists(Path.Combine(packageDir, QuiverLauncherPaths.MoveMarkerFileName)).Should().BeFalse();
            QuiverLauncherPaths.ResolveUnixUserDataRoot(packageDir, target).Should().Be(Path.GetFullPath(target));
        }
        finally
        {
            Directory.Delete(packageDir, recursive: true);
            Directory.Delete(Path.GetDirectoryName(target)!, recursive: true);
        }
    }

    [Fact]
    public void A_move_cut_short_after_copying_uses_the_copy_and_finishes_on_next_start()
    {
        var packageDir = TempDir("QuiverPkg");
        var target = TempDir("QuiverTarget");
        try
        {
            QuiverLauncherPaths.DirectoryWritableTester = null;
            Write(Path.Combine(packageDir, "settings.json"), "old");
            Write(Path.Combine(target, "settings.json"), "copied");
            Write(Path.Combine(packageDir, QuiverLauncherPaths.MoveMarkerFileName), "copied");

            QuiverLauncherPaths.ResolveUnixUserDataRoot(packageDir, target).Should().Be(Path.GetFullPath(target));
            QuiverLauncherPaths.CompletePendingDataMove(packageDir, target);

            File.ReadAllText(Path.Combine(target, "settings.json")).Should().Be("copied");
            File.Exists(Path.Combine(packageDir, "settings.json")).Should().BeFalse();
            File.Exists(Path.Combine(packageDir, QuiverLauncherPaths.MoveMarkerFileName)).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(packageDir, recursive: true);
            Directory.Delete(target, recursive: true);
        }
    }

    [Fact]
    public void A_move_cut_short_while_copying_starts_the_copy_again()
    {
        var packageDir = TempDir("QuiverPkg");
        var target = TempDir("QuiverTarget");
        try
        {
            QuiverLauncherPaths.DirectoryWritableTester = null;
            Write(Path.Combine(packageDir, "settings.json"), "settings");
            Write(Path.Combine(packageDir, "apps.json"), "apps");
            Write(Path.Combine(target, ".moving-in", "settings.json"), "partial");
            Write(Path.Combine(packageDir, QuiverLauncherPaths.MoveMarkerFileName), "requested");

            QuiverLauncherPaths.ResolveUnixUserDataRoot(packageDir, target).Should().Be(Path.GetFullPath(packageDir));
            QuiverLauncherPaths.CompletePendingDataMove(packageDir, target);

            File.ReadAllText(Path.Combine(target, "settings.json")).Should().Be("settings");
            File.ReadAllText(Path.Combine(target, "apps.json")).Should().Be("apps");
            File.Exists(Path.Combine(packageDir, "settings.json")).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(packageDir, recursive: true);
            Directory.Delete(target, recursive: true);
        }
    }

    [Fact]
    public void A_move_never_replaces_a_library_already_in_the_user_folder()
    {
        var packageDir = TempDir("QuiverPkg");
        var target = TempDir("QuiverTarget");
        try
        {
            QuiverLauncherPaths.DirectoryWritableTester = null;
            Write(Path.Combine(packageDir, "settings.json"), "beside");
            Write(Path.Combine(target, "settings.json"), "existing");
            Write(Path.Combine(packageDir, QuiverLauncherPaths.MoveMarkerFileName), "requested");

            QuiverLauncherPaths.CompletePendingDataMove(packageDir, target);

            File.ReadAllText(Path.Combine(target, "settings.json")).Should().Be("existing");
            File.ReadAllText(Path.Combine(packageDir, "settings.json")).Should().Be("beside");
            File.Exists(Path.Combine(packageDir, QuiverLauncherPaths.MoveMarkerFileName)).Should().BeFalse();
            QuiverLauncherPaths.ResolveUnixUserDataRoot(packageDir, target).Should().Be(Path.GetFullPath(packageDir));
        }
        finally
        {
            Directory.Delete(packageDir, recursive: true);
            Directory.Delete(target, recursive: true);
        }
    }
}
