using QuiverLauncher.Core.Services;

namespace QuiverLauncher.Services
{
    public sealed class QuiverLauncherProfile : LauncherProfile
    {
        public static QuiverLauncherProfile Instance { get; } = new();

        public override string DisplayName => "Fletch";
        public override string ApplicationId => "Fletch";
        public override string Repository => "Aknaru404/Fletch";
        public override string ExecutableName => "Fletch";
        public override string DefaultInstallFolderName => "Apps";
        public override string UserAgent => "Fletch/1.0 (+https://github.com/Aknaru404/Fletch)";
        public override string CliUserAgent => "Fletch-CLI";
        public override string UpdaterUserAgent => "Fletch-Updater";
        public override string SteamTag => "Fletch";
    }
}