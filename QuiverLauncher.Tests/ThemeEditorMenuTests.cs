using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using QuiverLauncher.Services;

namespace QuiverLauncher.Tests;

public class ThemeEditorMenuTests
{
    [AvaloniaFact]
    public void Sub_menus_use_the_theme_colors()
    {
        var themeColor = Color.Parse("#cfe0f5");
        var host = new Border { Width = 400, Height = 300 };
        new ThemeEditor(_ => themeColor, (_, _) => { }, new LauncherDialogLifetime(CancellationToken.None, action => action()))
            .ApplyResources(host.Resources, themeColor, Color.Parse("#1e3a5f"));
        var customize = new MenuItem { Header = "Customize", Items = { new MenuItem { Header = "Edit Entry" } } };
        var menu = new ContextMenu { Items = { new MenuItem { Header = "Download" }, customize } };
        host.ContextMenu = menu;
        var window = new Window { Width = 800, Height = 600, Content = host };

        try
        {
            window.Show();
            menu.Open(host);
            Dispatcher.UIThread.RunJobs();
            customize.IsSubMenuOpen = true;
            Dispatcher.UIThread.RunJobs();

            var popup = customize.GetVisualDescendants().OfType<Popup>().Single(p => p.Name == "PART_Popup");
            var surface = popup.Child!.GetSelfAndVisualDescendants().OfType<Border>().First(b => b.Background != null);
            surface.Background.Should().BeAssignableTo<ISolidColorBrush>()
                .Which.Color.Should().Be(themeColor);
            var edit = (MenuItem)customize.Items[0]!;
            edit.Foreground.Should().BeAssignableTo<ISolidColorBrush>()
                .Which.Color.Should().Be(((ISolidColorBrush)host.Resources["ThemeText"]!).Color);
        }
        finally
        {
            menu.Close();
            window.Close();
        }
    }
}
