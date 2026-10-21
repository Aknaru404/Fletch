using Avalonia;
using Avalonia.iOS;
using Avalonia.Threading;
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
        RegisterCrashLogging();
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }

    // Shared code logs crashes only in Debug builds. Release builds on device write crash.log to Documents,
    // which the owner can open in the Files app.
    private static void RegisterCrashLogging()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                CrashLog.Log("AppDomain.UnhandledException", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
            CrashLog.Log("TaskScheduler.UnobservedTaskException", e.Exception);
        Dispatcher.UIThread.UnhandledException += (_, e) =>
            CrashLog.LogFromUiThread("Dispatcher.UIThread.UnhandledException", e.Exception);
    }
}
