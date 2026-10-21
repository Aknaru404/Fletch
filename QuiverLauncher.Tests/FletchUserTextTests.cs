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
