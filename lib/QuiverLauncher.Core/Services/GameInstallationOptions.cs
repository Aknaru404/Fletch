namespace QuiverLauncher.Core.Services;

public sealed class GameInstallationOptions
{
    public static GameInstallationOptions Default { get; } = new();

    public IReadOnlyCollection<string> AdditionalMetadataFileNames { get; init; } = Array.Empty<string>();

    public Action<string>? Log { get; init; }

    /// <summary>
    /// Optional 0–1 progress for archive extraction (after the download has finished).
    /// </summary>
    public IProgress<double>? ExtractProgress { get; init; }

    /// <summary>
    /// The install folder isn't one Quiver made for this app (the player located it, or it's shared with other
    /// programs), so an AppImage update removes only <see cref="PreviousInstallFileName"/>, never the folder's
    /// other AppImages.
    /// </summary>
    public bool IsSharedFolder { get; init; }

    /// <summary>The file Quiver last installed this app from, when it recorded one.</summary>
    public string? PreviousInstallFileName { get; init; }

    /// <summary>
    /// Shows the player the license agreement a download asks them to accept (the download's file name, the
    /// agreement) and returns whether they accept it. Without it, such a download isn't installed.
    /// </summary>
    public Func<string, string, Task<bool>>? AcceptLicenseAsync { get; init; }
}
