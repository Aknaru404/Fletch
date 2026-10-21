using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FluentAssertions;
using QuiverLauncher.Services;
using QuiverLauncher.Views;

namespace QuiverLauncher.Tests;

/// <summary>
/// PLAN D13: text the iOS app can show says Fletch (or nothing), never Quiver. Desktop-only text stays as upstream.
/// </summary>
public class FletchUserTextTests
{
    private static string RepoRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    // The views iOS reaches, or that T2.5 rebranded.
    public static TheoryData<string> IosReachableViews() => new()
    {
        "MainView.axaml",
        "Views/SettingsView.axaml",
        "Views/LibraryView.axaml",
        "Views/BrowseView.axaml",
        "Views/BrowseDetailsView.axaml",
    };

    // Shown only on desktop (IsDesktopPlatform): the tray and kiosk settings, left as upstream (D13).
    private static readonly string[] DesktopOnly =
    [
        "Keep Quiver Launcher running in the notification area",
        "A cabinet should also make Quiver the Windows shell",
        "Hide Quiver Window Controls",
        "Adjust the size of text and controls in Quiver.",
    ];

    private static readonly Regex UserVisibleAttribute = new(
        @"\b(?:Text|Content|Title|ToolTip\.Tip|Watermark|PlaceholderText)=""(?<value>[^""]*)""", RegexOptions.Compiled);

    [Theory]
    [MemberData(nameof(IosReachableViews))]
    public void Ios_reachable_views_show_no_Quiver_text(string relativePath)
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot, relativePath));

        var offending = UserVisibleAttribute.Matches(xaml)
            .Select(m => m.Groups["value"].Value)
            .Where(value => value.Contains("quiver", StringComparison.OrdinalIgnoreCase))
            .Where(value => !value.StartsWith("avares://", StringComparison.Ordinal))
            .Where(value => !DesktopOnly.Any(value.Contains))
            .ToList();

        offending.Should().BeEmpty($"{relativePath} must not show Quiver on iOS (TRADEMARKS.md, PLAN D13)");
    }

    [Fact]
    public void Launcher_messages_name_Fletch()
    {
        string[] messages =
        [
            AppUpdateReviewMessages.FormatQuiverOnlyUpdateMessage("v1.2.0"),
            LauncherUpdateWorkflow.UsageDataTitle,
            LauncherUpdateWorkflow.UsageDataQuestion,
            LauncherUpdateWorkflow.FirstRunWelcomeTitle,
            LauncherUpdateWorkflow.FirstRunWelcomeMessage,
            ManualAppFolderService.InstructionContents,
            CatalogReleases.NotSeen,
        ];

        messages.Should().NotContain(m => m.Contains("Quiver", StringComparison.OrdinalIgnoreCase));
        AppUpdateReviewMessages.FormatQuiverOnlyUpdateMessage("v1.2.0")
            .Should().Be("Fletch update v1.2.0 is available.\n\nUpdate Fletch now?");
    }

    [Fact]
    public void GitHub_token_link_names_Fletch()
    {
        // Settings → Create token and the rate-limit dialog open this link on iOS.
        GitHubTokenSetupGuide.CreateTokenUrl.Should().Be(
            "https://github.com/settings/tokens/new?description=Fletch+Token+for+increased+API+rate+limits");
    }

    [Fact]
    public void First_run_welcome_mentions_the_catalog_only_when_there_is_one()
    {
        LauncherUpdateWorkflow.FirstRunWelcomeMessage.Should().Be(LauncherUpdateWorkflow.GetFirstRunWelcomeMessage(hasAppCatalog: false));
        LauncherUpdateWorkflow.GetFirstRunWelcomeMessage(hasAppCatalog: false)
            .Should().NotContainEquivalentOf("catalog").And.Contain("GitHub, GitLab or Codeberg");
        LauncherUpdateWorkflow.GetFirstRunWelcomeMessage(hasAppCatalog: true).Should().Contain("Browse the catalog");
    }

    [Fact]
    public void Update_Fletch_button_is_affirmative_for_the_gamepad()
    {
        // LauncherPromptService and App.Updates label the button with this; the gamepad must treat it as Yes.
        AppUpdateReviewMessages.UpdateLauncherButtonLabel.Should().Be("Update Fletch");
        GamepadModalDialogNavigation.IsAffirmativeDialogButtonLabel(AppUpdateReviewMessages.UpdateLauncherButtonLabel).Should().BeTrue();
    }

    // Source files whose "Quiver" literals D13 leaves as upstream, because iOS never runs or shows them.
    private static readonly Dictionary<string, string[]?> UpstreamQuiverText = new()
    {
        // Android only.
        ["QuiverLauncher.Android/"] = null,
        ["Services/AndroidLauncherPackageValidation.cs"] = null,
        ["Services/AndroidLauncherUpdater.cs"] = null,
        // Command line only.
        ["Services/CLIHandler.cs"] = null,
        ["Services/IGameDownloadDialogs.cs"] = null,
        // Desktop only: tray, macOS menu, Velopack self-update, data move, Windows installers, Flatpak, shortcuts.
        ["App.axaml.cs"] = null,
        ["App.MacMenu.cs"] = null,
        ["Services/LauncherUpdateBackup.cs"] = null,
        ["Services/LauncherUpdateFailure.cs"] = null,
        ["Services/QuiverLauncherPaths.cs"] = null,
        ["Services/UserDataRescue.cs"] = null,
        ["Services/WindowsInstallerService.cs"] = null,
        ["Services/GameDialogService.cs"] = null,
        ["Services/GameInstallLocationService.cs"] = null,
        ["Services/GameLaunchService.cs"] = null,
        ["Services/FlatpakService.cs"] = null,
        ["Services/GameShortcutLaunch.cs"] = null,
        ["Services/ShortcutHelper.cs"] = null,
        ["Views/ShortcutExecutableDialog.cs"] = null,
        // Mods need a mods folder, which mobile lacks (PlatformCapabilities.SupportsModsFolder).
        ["Services/Mods/Providers/GameBanana/GameBananaModProvider.cs"] = null,
        // Desktop delete: mobile uninstalls through its own branch before these messages.
        ["Services/LibraryActions.cs"] =
        [
            "Quiver Launcher will attempt to move game files to your system's Recycle Bin / Trash",
            "so Quiver Launcher leaves the folder",
            "Only the file Quiver Launcher downloaded there",
            "and Quiver Launcher stops using the folder.",
        ],
    };

    // Internal names, not text: resource URIs, the assembly/executable name, and type names in interpolation holes.
    private static readonly Regex InternalQuiverName = new(
        @"avares://QuiverLauncher/|\bQuiverLauncher(?:Profile|Paths|_SKIP_UPDATES)?\b|\bQuiverCatalogClient\b", RegexOptions.Compiled);
    private static readonly Regex RawLiteral = new(@"""{3,}[\s\S]*?""{3,}", RegexOptions.Compiled);
    private static readonly Regex Literal = new(@"""(?:[^""\\\r\n]|\\.)*""", RegexOptions.Compiled);

    [Fact]
    public void Source_strings_do_not_say_Quiver_outside_upstream_only_files()
    {
        var offending = new List<string>();
        foreach (var file in Directory.EnumerateFiles(RepoRoot, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(RepoRoot, file).Replace('\\', '/');
            if (relative.Split('/').Any(part => part is "bin" or "obj" or "QuiverLauncher.Tests"))
                continue;
            var allowed = UpstreamQuiverText.FirstOrDefault(entry => relative.StartsWith(entry.Key, StringComparison.Ordinal));
            if (allowed.Key != null && allowed.Value == null)
                continue;

            var source = File.ReadAllText(file);
            var literals = RawLiteral.Matches(source).Select(m => m.Value).ToList();
            literals.AddRange(RawLiteral.Replace(source, "").Split('\n')
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                .SelectMany(line => Literal.Matches(line).Select(m => m.Value)));
            offending.AddRange(literals
                .Where(literal => InternalQuiverName.Replace(literal, "").Contains("Quiver", StringComparison.Ordinal))
                .Where(literal => allowed.Value?.Any(literal.Contains) != true)
                .Select(literal => $"{relative}: {literal}"));
        }

        offending.Should().BeEmpty("iOS must not show Quiver (TRADEMARKS.md, PLAN D13); desktop/Android-only files are listed above");
    }

    [Fact]
    public void Upstream_community_links_have_their_own_flag_and_are_off()
    {
        PlatformCapabilities.HasUpstreamCommunityLinks.Should().BeFalse();
        PlatformCapabilities.HasAppCatalog.Should().BeFalse();
    }

    [AvaloniaFact]
    public async Task Sidebar_says_Fletch_and_settings_hide_the_custom_app_list()
    {
        var view = new MainView(new() { SettingsStore = new Store(), InitializeOnOpen = false, EnableInput = false, EnableMusic = false });
        var window = new Window { Content = view, Width = 1200, Height = 720 };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            view.FindControl<StackPanel>("SidebarLogoPanel")!.GetLogicalDescendantsText()
                .Should().Contain("FLETCH").And.NotContain(t => t.Contains("QUIVER"));
            // The list feeds the App Catalog, which Fletch doesn't have (PlatformCapabilities.HasAppCatalog).
            view.FindControl<SettingsView>("SettingsPanel")!
                .FindControl<StackPanel>("CustomAppListPanel")!.IsVisible.Should().BeFalse();
        }
        finally { window.Close(); await view.ShutdownAsync(); }
    }

    private sealed class Store : ISettingsStore
    {
        public AppSettings Current { get; private set; } = new()
        {
            FirstStartup = false, EnableGamepadInput = false,
            AppsPath = Path.Combine(Path.GetTempPath(), "fletch-text-tests", Guid.NewGuid().ToString("N")),
        };
        public AppSettings Load() => Current;
        public void Save(AppSettings settings) => Current = settings;
    }
}

internal static class LogicalTextExtensions
{
    public static IEnumerable<string> GetLogicalDescendantsText(this Avalonia.LogicalTree.ILogical root) =>
        Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(root).OfType<TextBlock>().Select(t => t.Text ?? "");
}
