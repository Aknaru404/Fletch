using Avalonia;
using Avalonia.iOS;
using Foundation;
using QuiverLauncher;
using QuiverLauncher.Services;

namespace Fletch.iOS;

[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // The app bundle is read-only; keep the library in Documents, which the Files app can show.
        QuiverLauncherPaths.OverrideUserDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        QuiverLauncherPaths.EnsureUserDataRootExists();
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
