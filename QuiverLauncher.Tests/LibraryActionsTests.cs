using FluentAssertions;
using QuiverLauncher.Core.Models;
using QuiverLauncher.Models;
using QuiverLauncher.Services;
using QuiverLauncher.ViewModels;

namespace QuiverLauncher.Tests;

public class LibraryActionsTests
{
    private sealed class Store(string directory) : ISettingsStore
    {
        public AppSettings Current { get; } = new() { AppsPath = Path.Combine(directory, "Apps") };
        public AppSettings Load() => Current;
        public void Save(AppSettings settings) { }
    }
    [Fact]
    public async Task Closing_session_while_remove_confirmation_is_pending_prevents_catalog_mutation()
    {
        var directory = Path.Combine(Path.GetTempPath(), "quiver-action-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var store = new Store(directory);
        var catalog = new AppCatalogService(dataDirectory: directory);
        var game = new GameInfo { Name = "Example", Repository = "owner/example", FolderName = "Example", IsInLocalAppsJson = true };
        await catalog.SaveLocalAppsAsync([game]);
        using var manager = new GameManager(store, catalogService: catalog);
        using var library = new LibraryViewModel(manager, new SettingsViewModel(store));
        // Installed, so removing it asks first.
        Directory.CreateDirectory(Path.Combine(directory, "Apps", "Example"));
        var session = new LauncherSession();
        var prompt = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var actions = new LibraryActions(manager, new LibraryPersistenceService(manager), library.Settings, session, library,
            () => throw new Exception("Unexpected picker"), (_, _, _, _) => { entered.SetResult(); return prompt.Task; },
            _ => throw new Exception("Unexpected URL"), () => throw new Exception("Unexpected library change"));
        var pending = actions.RemoveEntryAsync(game);
        await entered.Task;
        var shutdown = session.DisposeAsync().AsTask();
        shutdown.IsCompleted.Should().BeFalse();
        prompt.SetResult(true);
        await pending;
        await shutdown;
        (await catalog.LoadLocalAppsAsync()).Should().ContainSingle(g => g.Repository == "owner/example");
    }

    [Fact]
    public async Task An_app_that_isnt_installed_is_removed_without_asking()
    {
        var directory = Path.Combine(Path.GetTempPath(), "quiver-action-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var store = new Store(directory);
        var catalog = new AppCatalogService(dataDirectory: directory);
        var game = new GameInfo { Name = "Example", Repository = "owner/example", FolderName = "Example", IsInLocalAppsJson = true };
        await catalog.SaveLocalAppsAsync([game]);
        using var manager = new GameManager(store, catalogService: catalog);
        using var library = new LibraryViewModel(manager, new SettingsViewModel(store));
        var changed = false;
        var actions = new LibraryActions(manager, new LibraryPersistenceService(manager), library.Settings, new LauncherSession(), library,
            () => throw new Exception("Unexpected picker"), (_, _, _, _) => throw new Exception("Unexpected prompt"),
            _ => throw new Exception("Unexpected URL"), () => { changed = true; return Task.CompletedTask; });

        await actions.RemoveEntryAsync(game);

        (await catalog.LoadLocalAppsAsync()).Should().BeEmpty();
        changed.Should().BeTrue();
        TestFixtures.CleanupDirectory(directory);
    }

    [Fact]
    public async Task Kiosk_lock_refuses_remove_without_prompting()
    {
        var directory = Path.Combine(Path.GetTempPath(), "quiver-action-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var store = new Store(directory);
        store.Current.KioskMode = true;
        var catalog = new AppCatalogService(dataDirectory: directory);
        var game = new GameInfo { Name = "Example", Repository = "owner/example", FolderName = "Example", IsInLocalAppsJson = true };
        await catalog.SaveLocalAppsAsync([game]);
        using var manager = new GameManager(store, catalogService: catalog);
        using var library = new LibraryViewModel(manager, new SettingsViewModel(store));
        library.Settings.KioskLocked.Should().BeTrue();
        var actions = new LibraryActions(manager, new LibraryPersistenceService(manager), library.Settings, new LauncherSession(), library,
            () => throw new Exception("Unexpected picker"), (_, _, _, _) => throw new Exception("Unexpected prompt"),
            _ => throw new Exception("Unexpected URL"), () => throw new Exception("Unexpected catalog refresh"));
        await actions.RemoveEntryAsync(game);
        KioskLock.AllowsLibraryAction(Views.LibraryActionKind.LaunchGameMenu).Should().BeTrue();
        KioskLock.AllowsLibraryAction(Views.LibraryActionKind.RemoveGameEntry).Should().BeFalse();
        (await catalog.LoadLocalAppsAsync()).Should().ContainSingle(g => g.Repository == "owner/example");
    }
    [Fact]
    public async Task Runner_prompt_respects_already_cancelled_host_lifetime()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var action = () => GameDialogService.ShowLinuxWindowsRunnerDialogAsync("unused", cancellationToken: cancellation.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Msi_uninstall_opens_windows_settings_without_deleting_any_files()
    {
        if (!OperatingSystem.IsWindows()) return;
        var directory = Path.Combine(Path.GetTempPath(), "quiver-action-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var store = new Store(directory);
            var catalog = new AppCatalogService(dataDirectory: directory);
            using var manager = new GameManager(store, catalogService: catalog);
            using var library = new LibraryViewModel(manager, new SettingsViewModel(store));
            await using var session = new LauncherSession();
            var game = new GameInfo { Name = "MSI", FolderName = "MSI" };
            var metadata = game.GetInstallPath(store.Current.AppsPath);
            var executable = Path.Combine(directory, "external.exe");
            File.WriteAllText(executable, "keep installed app");
            WindowsInstallerService.WriteReceipt(metadata, new("Linked", "v1", executable));
            var opened = new List<string>();
            var actions = new LibraryActions(manager, new LibraryPersistenceService(manager), library.Settings, session, library,
                () => throw new Exception("Unexpected picker"), (_, _, _, _) => throw new Exception("Unexpected delete prompt"),
                opened.Add, () => throw new Exception("Unexpected catalog refresh"));

            await actions.UninstallAsync(game);

            opened.Should().Equal("ms-settings:appsfeatures");
            WindowsInstallerService.HasReceipt(metadata).Should().BeTrue();
            File.ReadAllText(executable).Should().Be("keep installed app");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
    [Fact]
    public async Task Deleting_an_app_in_a_located_folder_leaves_the_folder_and_its_other_files()
    {
        var directory = Path.Combine(Path.GetTempPath(), "quiver-action-tests", Guid.NewGuid().ToString("N"));
        var shared = Path.Combine(directory, "AppImages");
        Directory.CreateDirectory(shared);
        try
        {
            var store = new Store(directory);
            var catalog = new AppCatalogService(dataDirectory: directory);
            var game = new GameInfo { Name = "Recomp", Repository = "owner/recomp", FolderName = "Recomp", InstallPath = shared, IsInLocalAppsJson = true };
            await catalog.SaveLocalAppsAsync([game]);
            var others = new[] { "QuiverLauncher-x86_64.AppImage", "OtherRecomp.AppImage", "Recomp.AppImage" }
                .Select(name => Path.Combine(shared, name)).ToArray();
            foreach (var file in others) File.WriteAllText(file, "keep");
            File.WriteAllText(Path.Combine(shared, "version.txt"), "v1.0.0");
            using var manager = new GameManager(store, catalogService: catalog);
            using var library = new LibraryViewModel(manager, new SettingsViewModel(store));
            game.GameManager = manager;
            game.Status = GameStatus.Installed;
            string? asked = null;
            var actions = new LibraryActions(manager, new LibraryPersistenceService(manager), library.Settings, new LauncherSession(), library,
                () => throw new Exception("Unexpected picker"), (message, _, _, _) => { asked = message; return Task.FromResult(true); },
                _ => throw new Exception("Unexpected URL"), () => Task.CompletedTask);

            await actions.UninstallAsync(game);

            asked.Should().Contain(shared);
            others.Should().OnlyContain(file => File.Exists(file));
            File.Exists(Path.Combine(shared, "version.txt")).Should().BeFalse();
            game.InstallPath.Should().BeNull();
            (await catalog.LoadLocalAppsAsync()).Should().ContainSingle(g => g.Repository == "owner/recomp" && g.InstallPath == null);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void Located_folder_removal_trashes_only_the_file_quiver_installed()
    {
        var folder = Path.Combine(Path.GetTempPath(), "quiver-action-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "QuiverLauncher-x86_64.AppImage"), "keep");
            File.WriteAllText(Path.Combine(folder, "Game-1.0.AppImage"), "installed");
            File.WriteAllText(Path.Combine(folder, InstalledBuild.FileNameOnDisk), $"Game-1.0.AppImage\n{new string('a', 64)}\n");
            var trashed = new List<string>();

            LocatedInstallRemoval.Remove(folder, folderHasOtherApps: false, trashed.Add);

            trashed.Should().Equal(Path.Combine(folder, "Game-1.0.AppImage"));
            File.Exists(Path.Combine(folder, "QuiverLauncher-x86_64.AppImage")).Should().BeTrue();
            File.Exists(Path.Combine(folder, InstalledBuild.FileNameOnDisk)).Should().BeFalse();
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void Located_folder_removal_touches_nothing_when_another_app_uses_the_folder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "quiver-action-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "Other-1.0.AppImage"), "keep");
            File.WriteAllText(Path.Combine(folder, InstalledBuild.FileNameOnDisk), $"Other-1.0.AppImage\n{new string('a', 64)}\n");
            File.WriteAllText(Path.Combine(folder, "version.txt"), "v1.0.0");

            LocatedInstallRemoval.Remove(folder, folderHasOtherApps: true, _ => throw new Exception("Unexpected trash"));

            Directory.GetFiles(folder).Should().HaveCount(3);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }
}
