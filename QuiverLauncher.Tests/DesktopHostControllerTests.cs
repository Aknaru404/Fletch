using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FluentAssertions;
using QuiverLauncher.Services;
using QuiverLauncher.Views;

namespace QuiverLauncher.Tests;

public class DesktopHostControllerTests
{
    private sealed class Store(bool showTopBar) : ISettingsStore
    {
        public AppSettings Current { get; private set; } = new()
        {
            ShowOSTopBar = showTopBar, FirstStartup = false, EnableGamepadInput = false,
            AppsPath = Path.Combine(Path.GetTempPath(), "quiver-window-tests", Guid.NewGuid().ToString("N"))
        };
        public AppSettings Load() => Current;
        public void Save(AppSettings settings) => Current = settings;
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Window_chrome_follows_saved_setting_and_live_checkbox_changes(bool initiallyVisible)
    {
        var store = new Store(initiallyVisible);
        var view = new MainView(new() { SettingsStore = store, InitializeOnOpen = false, EnableInput = false, EnableMusic = false });
        var window = new Window { Content = view };
        using var controller = new DesktopHostController(window, view);
        void AssertChrome(bool visible)
        {
            window.WindowDecorations.Should().Be(visible ? WindowDecorations.Full : WindowDecorations.BorderOnly);
            window.ExtendClientAreaToDecorationsHint.Should().Be(!visible);
        }
        try
        {
            AssertChrome(initiallyVisible);
            window.Show();
            var checkbox = view.FindControl<SettingsView>("SettingsPanel")!.FindControl<CheckBox>("ShowOSTopBarCheckBox")!;
            foreach (var visible in new[] { !initiallyVisible, initiallyVisible })
            {
                checkbox.IsChecked = visible;
                Dispatcher.UIThread.RunJobs();
                store.Current.ShowOSTopBar.Should().Be(visible);
                AssertChrome(visible);
            }
            controller.Dispose();
            view.SettingsModel.ShowOSTopBar = !initiallyVisible;
            AssertChrome(initiallyVisible);
        }
        finally { window.Close(); await view.ShutdownAsync(); }
    }

    [AvaloniaFact]
    public async Task Hiding_window_controls_follows_the_OS_top_bar_and_keeps_them_in_fullscreen()
    {
        var store = new Store(true);
        var view = new MainView(new() { SettingsStore = store, InitializeOnOpen = false, EnableInput = false, EnableMusic = false });
        var window = new Window { Content = view, Width = 1200, Height = 720 };
        using var controller = new DesktopHostController(window, view);
        var settings = view.FindControl<SettingsView>("SettingsPanel")!;
        void AssertControls(bool visible)
        {
            foreach (var name in new[] { "MinimizeButton", "ToggleMaximizeButton", "CloseLauncherButton" })
                view.FindControl<Button>(name)!.IsVisible.Should().Be(visible, name);
            view.FindControl<StackPanel>("HeaderFixedActions")!.Margin.Right.Should().Be(visible ? 0 : 16);
        }
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            AssertControls(true);

            settings.FindControl<CheckBox>("HideWindowControlsCheckBox")!.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            store.Current.HideWindowControls.Should().BeTrue();
            AssertControls(false);

            window.WindowState = WindowState.FullScreen;
            Dispatcher.UIThread.RunJobs();
            AssertControls(true);
            window.WindowState = WindowState.Normal;
            Dispatcher.UIThread.RunJobs();
            AssertControls(false);

            var topBar = settings.FindControl<CheckBox>("ShowOSTopBarCheckBox")!;
            topBar.IsChecked = false;
            Dispatcher.UIThread.RunJobs();
            ((Control)settings.FindControl<CheckBox>("HideWindowControlsCheckBox")!.Parent!).IsVisible.Should().BeFalse();
            AssertControls(true);
            topBar.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            AssertControls(false);
        }
        finally { window.Close(); await view.ShutdownAsync(); }
    }
}
