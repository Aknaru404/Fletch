namespace QuiverLauncher.Services;

/// <summary>
/// Remote announcement banner. Upstream read announcement.json from its own repository; Fletch fetches
/// no upstream content (PLAN D12), so there is no feed and no banner unless a caller passes a URL.
/// </summary>
public static class AnnouncementDefaults
{
    public static string? RemoteUrl => null;
}
