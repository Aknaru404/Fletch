namespace QuiverLauncher.Services;

/// <summary>
/// Runtime feature switches for desktop vs mobile (Android, iOS).
/// </summary>
public static class PlatformCapabilities
{
    public static bool IsMobile => OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();

    public static bool IsDesktop => !IsMobile;

    public static string InstalledAppRemovalLabel => IsMobile ? "Uninstall" : "Delete";

    public static bool SupportsTray => !IsMobile;

    public static bool SupportsVelopack => !IsMobile;

    public static bool SupportsFolderInstall => !IsMobile;

    public static bool SupportsWine => OperatingSystem.IsLinux() && !IsMobile;

    public static bool SupportsSteamShortcuts =>
        !IsMobile && (OperatingSystem.IsWindows() || OperatingSystem.IsLinux());

    public static bool SupportsModsFolder => !IsMobile;

    public static bool SupportsGamepadSdl => !IsMobile;

    /// <summary>
    /// The upstream App Catalog. Off in every Fletch build: it sends no data to the upstream author (PLAN D12), so the
    /// UI offers no way into it.
    /// </summary>
    public static bool HasAppCatalog => false;

    /// <summary>The upstream community links (website, Discord, Patreon). Off in every Fletch build (PLAN D12).</summary>
    public static bool HasUpstreamCommunityLinks => false;
}
