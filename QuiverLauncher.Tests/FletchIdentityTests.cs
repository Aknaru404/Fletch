using FluentAssertions;
using QuiverLauncher.Services;

namespace QuiverLauncher.Tests;

/// <summary>The fork's identifiers (TRADEMARKS.md): nothing that identifies the app says Quiver.</summary>
public class FletchIdentityTests
{
    const string UserAgent = "Fletch/1.0 (+https://github.com/Aknaru404/Fletch)";

    [Fact]
    public void Profile_identifies_the_app_as_Fletch()
    {
        var profile = QuiverLauncherProfile.Instance;
        profile.DisplayName.Should().Be("Fletch");
        profile.ApplicationId.Should().Be("Fletch");
        profile.Repository.Should().Be("Aknaru404/Fletch");
        profile.ExecutableName.Should().Be("Fletch");
        profile.UserAgent.Should().Be(UserAgent);
        profile.CliUserAgent.Should().Be("Fletch-CLI");
        profile.UpdaterUserAgent.Should().Be("Fletch-Updater");
        profile.SteamTag.Should().Be("Fletch");
        profile.DefaultInstallFolderName.Should().Be("Apps");
    }

    [Fact]
    public void Launcher_updates_come_from_the_Fletch_repository() =>
        VelopackUpdateService.GitHubRepoUrl.Should().Be("https://github.com/Aknaru404/Fletch");

    [Fact]
    public void Data_folder_and_updater_log_are_named_Fletch()
    {
        QuiverLauncherPaths.AppName.Should().Be("Fletch");
        Path.GetFileName(QuiverLauncherPaths.GetOsFallbackUserDataRoot()).Should().Be("Fletch");
        Path.GetFileName(LauncherUpdateFailure.DefaultLogPath(null)).Should().Be("velopack_Fletch.log");
    }

    [Fact]
    public void Readme_requests_send_the_Fletch_user_agent() =>
        RepositoryReadmeService.UserAgent.Should().Be(UserAgent);
}
