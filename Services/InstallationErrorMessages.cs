using System.Runtime.InteropServices;

namespace QuiverLauncher.Services;

public static class InstallationErrorMessages
{
    private const string WindowsDefenderGuidance =
        "\n\nIf Windows Defender blocked this download, open Windows Security → Protection history, find the blocked item, and choose Allow if you believe it is safe.";

    private const string FileLockGuidance =
        "\n\nWindows may still be scanning the downloaded archive. Wait a few seconds and try installing again.";

    // Windows error codes as an HRESULT: they say the same thing whatever language Windows shows its messages in.
    private const int VirusInfected = unchecked((int)0x800700E1);
    private const int VirusDeleted = unchecked((int)0x800700E2);
    private const int SharingViolation = unchecked((int)0x80070020);
    private const int LockViolation = unchecked((int)0x80070021);

    public static string FormatInstallationError(string gameName, Exception exception) =>
        Format(gameName, exception.Message,
            IsLikelyWindowsDefenderBlock(exception.Message) || HasErrorCode(exception, VirusInfected, VirusDeleted),
            IsLikelyFileLock(exception.Message) || HasErrorCode(exception, SharingViolation, LockViolation));

    public static string FormatInstallationError(string gameName, string errorMessage) =>
        Format(gameName, errorMessage, IsLikelyWindowsDefenderBlock(errorMessage), IsLikelyFileLock(errorMessage));

    private static string Format(string gameName, string errorMessage, bool defenderBlock, bool fileLock)
    {
        var message = $"Error installing {gameName}: {errorMessage}";
        var windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        if (windows && defenderBlock)
            message += WindowsDefenderGuidance;
        else if (windows && fileLock)
            message += FileLockGuidance;

        return message;
    }

    internal static bool HasErrorCode(Exception exception, params int[] codes)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
            if (codes.Contains(current.HResult))
                return true;
        return false;
    }

    internal static bool IsLikelyWindowsDefenderBlock(string errorMessage) =>
        errorMessage.Contains("virus", StringComparison.OrdinalIgnoreCase)
        || errorMessage.Contains("potentially unwanted", StringComparison.OrdinalIgnoreCase)
        || errorMessage.Contains("Windows Defender", StringComparison.OrdinalIgnoreCase);

    internal static bool IsLikelyFileLock(string errorMessage) =>
        errorMessage.Contains("being used by another process", StringComparison.OrdinalIgnoreCase)
        || errorMessage.Contains("still locked by another process", StringComparison.OrdinalIgnoreCase)
        || errorMessage.Contains("lock violation", StringComparison.OrdinalIgnoreCase);
}
