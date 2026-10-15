using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using FluentAssertions;
using QuiverLauncher.Core.Models;
using QuiverLauncher.Core.Services;

namespace QuiverLauncher.Tests;

/// <summary>Releases that ship Linux builds as .tar.xz, .deb or .run files, and the update feeds beside them.</summary>
public sealed class LinuxPackageFormatTests : IDisposable
{
    const UnixFileMode Program = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
    const UnixFileMode Data = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
    const string Script = "#!/bin/sh\necho game\n";

    readonly string _root = Path.Combine(Path.GetTempPath(), "quiver-linux-formats-" + Guid.NewGuid().ToString("N"));
    string GamePath => Path.Combine(_root, "game");

    public LinuxPackageFormatTests() => Directory.CreateDirectory(_root);
    public void Dispose() => TestFixtures.CleanupDirectory(_root);

    [Theory]
    [InlineData("Taisei-1.4.6-linux-x86_64.tar.xz")]
    [InlineData("openttd-jgrpp-0.73.4-linux-generic-amd64.tar.xz")]
    [InlineData("game.txz")]
    [InlineData("game.tar.bz2")]
    [InlineData("project-eleven-win.tar.zst")]
    [InlineData("game.tgz")]
    public void Compressed_tarballs_are_installable_archives(string name)
    {
        GameInstallationService.IsTarballAsset(name).Should().BeTrue();
        GameInstallationService.HasRecognizedInstallExtension(name).Should().BeTrue();
        GameInstallationService.IsSingleFileExecutableAsset(name).Should().BeFalse();
    }

    [Theory]
    [InlineData("SonicAdventureRecompiled-0.50.0-beta.1-Linux.run")]
    [InlineData("doukutsu-rs_linux_1.0.0.x86_64.elf")]
    public void Linux_setups_and_programs_are_single_files(string name)
    {
        GameInstallationService.IsSingleFileExecutableAsset(name).Should().BeTrue();
        GameInstallationService.HasRecognizedInstallExtension(name).Should().BeTrue();
        PlatformAssetMatcher.MatchesPlatform(name, "Linux-X64").Should().BeTrue();
        PlatformAssetMatcher.MatchesPlatform(name, "Windows").Should().BeFalse();
    }

    [Theory]
    [InlineData("latest-linux.yml")]
    [InlineData("latest-mac.yml")]
    [InlineData("latest.YAML")]
    [InlineData("OpenRA-Dune-2000-playtest-x86_64.AppImage.zsync")]
    [InlineData("Relic of the Past Setup 0.19.3.exe.blockmap")]
    public void Update_feeds_beside_a_download_are_not_offered(string name)
    {
        DownloadAssetPolicy.IsAuxiliary(name).Should().BeTrue();
        var release = new GitHubRelease { assets = [new() { name = "rotp-linux.AppImage" }, new() { name = name }] };
        DownloadAssetPolicy.Select(release, "Linux-X64").Automatic!.name.Should().Be("rotp-linux.AppImage");
    }

    [Fact]
    public async Task A_run_setup_is_kept_as_a_program()
    {
        var download = Path.Combine(_root, "download");
        await File.WriteAllTextAsync(download, Script, TestContext.Current.CancellationToken);

        await GameInstallationService.InstallOrUpdateGameAsync(download, GamePath,
            "SonicAdventureRecompiled-0.50.0-beta.1-Linux.run", "v0.50.0-beta.1");

        var installed = Path.Combine(GamePath, "SonicAdventureRecompiled-0.50.0-beta.1-Linux.run");
        File.ReadAllText(installed).Should().Be(Script);
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(installed).Should().HaveFlag(UnixFileMode.UserExecute);
            GameInstallationService.FindExecutableCandidates(GamePath, SearchOption.AllDirectories, null, out _)
                .Should().Equal(installed);
        }
    }

    [Fact]
    public async Task A_tar_xz_installs_with_its_programs_executable()
    {
        var archive = SystemTarball("-cJf", "Taisei-1.4.6-linux-x86_64.tar.xz");

        await GameInstallationService.InstallOrUpdateGameAsync(archive, GamePath, Path.GetFileName(archive), "v1.4.6");

        File.ReadAllText(Path.Combine(GamePath, "taisei")).Should().Be(Script);
        File.GetUnixFileMode(Path.Combine(GamePath, "taisei")).Should().HaveFlag(UnixFileMode.UserExecute);
        File.ReadAllText(Path.Combine(GamePath, "data", "res.txt")).Should().Be("resources");
        File.ReadAllText(Path.Combine(GamePath, "version.txt")).Should().Be("v1.4.6");
    }

    [Fact]
    public void Without_system_tar_a_tar_xz_still_extracts_and_its_programs_are_executable()
    {
        var archive = SystemTarball("-cJf", "game.tar.xz");
        var destination = Path.Combine(_root, "extracted");

        GameInstallationService.ExtractTarballManaged(archive, destination);

        var program = Path.Combine(destination, "Taisei-1.4.6", "taisei");
        File.ReadAllText(program).Should().Be(Script);
        File.GetUnixFileMode(program).Should().HaveFlag(UnixFileMode.UserExecute);
        File.GetUnixFileMode(Path.Combine(destination, "Taisei-1.4.6", "data", "res.txt"))
            .Should().NotHaveFlag(UnixFileMode.UserExecute);
    }

    [Fact]
    public async Task A_deb_with_its_app_in_opt_installs_that_folder()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Debian packages install on Linux only.");
        var package = Path.Combine(_root, "gzdoom_4.14.2_amd64.deb");
        File.WriteAllBytes(package, Deb(TarGz(
            ("./usr/games/gzdoom", "#!/bin/bash\nexec /opt/gzdoom/gzdoom \"$@\"\n", Program),
            ("./usr/share/applications/gzdoom.desktop", "[Desktop Entry]\n", Data),
            ("./opt/gzdoom/gzdoom", Script, Program),
            ("./opt/gzdoom/gzdoom.pk3", "engine data", Data))));

        await GameInstallationService.InstallOrUpdateGameAsync(package, GamePath, "gzdoom_4.14.2_amd64.deb", "g4.14.2");

        File.ReadAllText(Path.Combine(GamePath, "gzdoom")).Should().Be(Script);
        File.GetUnixFileMode(Path.Combine(GamePath, "gzdoom")).Should().HaveFlag(UnixFileMode.UserExecute);
        File.ReadAllText(Path.Combine(GamePath, "gzdoom.pk3")).Should().Be("engine data");
        Directory.Exists(Path.Combine(GamePath, "usr")).Should().BeFalse("the /usr/games wrapper points at /opt, outside the app");
        GameInstallationService.FindExecutableCandidates(GamePath, SearchOption.AllDirectories, null, out _)
            .Should().Equal(Path.Combine(GamePath, "gzdoom"));
    }

    [Fact]
    public async Task A_deb_without_an_opt_folder_keeps_its_program_beside_its_shared_files()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Debian packages install on Linux only.");
        var package = Path.Combine(_root, "rigelengine_linux_x64.deb");
        File.WriteAllBytes(package, Deb(TarGz(
            ("./usr/bin/RigelEngine", Script, Program),
            ("./usr/share/rigelengine/data.txt", "data", Data))));

        await GameInstallationService.InstallOrUpdateGameAsync(package, GamePath, "rigelengine_linux_x64.deb", "v0.9.1");

        var program = Path.Combine(GamePath, "bin", "RigelEngine");
        File.ReadAllText(program).Should().Be(Script);
        File.GetUnixFileMode(program).Should().HaveFlag(UnixFileMode.UserExecute);
        File.ReadAllText(Path.Combine(GamePath, "share", "rigelengine", "data.txt")).Should().Be("data");
        GameInstallationService.FindExecutableCandidates(GamePath, SearchOption.AllDirectories, null, out _)
            .Should().Equal(program);
    }

    [Fact]
    public void A_deb_data_archive_is_found_after_its_control_archive()
    {
        var data = TarGz(("./usr/bin/game", Script, Program));
        var package = Path.Combine(_root, "game.deb");
        File.WriteAllBytes(package, Deb(data, controlLength: 3));

        var extracted = GameInstallationService.ExtractDebianDataArchive(package, _root);

        Path.GetFileName(extracted).Should().Be("data.tar.gz");
        File.ReadAllBytes(extracted).Should().Equal(data);
    }

    [Fact]
    public void A_file_that_is_not_a_deb_is_refused()
    {
        var package = Path.Combine(_root, "game.deb");
        File.WriteAllText(package, "<html>Not found</html>");

        var act = () => GameInstallationService.ExtractDebianDataArchive(package, _root);

        act.Should().Throw<InvalidDataException>().WithMessage("'game.deb' is not a Debian package.");
    }

    /// <summary>A tarball made by the system's tar, holding a program and a data file in one top folder.</summary>
    string SystemTarball(string createFlags, string name)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Uses the system tar and Unix file modes.");
        var source = Path.Combine(_root, "source");
        Directory.CreateDirectory(Path.Combine(source, "Taisei-1.4.6", "data"));
        File.WriteAllText(Path.Combine(source, "Taisei-1.4.6", "taisei"), Script);
        File.SetUnixFileMode(Path.Combine(source, "Taisei-1.4.6", "taisei"), Program);
        File.WriteAllText(Path.Combine(source, "Taisei-1.4.6", "data", "res.txt"), "resources");
        var archive = Path.Combine(_root, name);
        using var tar = Process.Start(new ProcessStartInfo("tar", [createFlags, archive, "-C", source, "Taisei-1.4.6"])
            { RedirectStandardError = true })!;
        var error = tar.StandardError.ReadToEnd();
        tar.WaitForExit();
        Assert.SkipWhen(tar.ExitCode != 0, $"The system tar can't write {name}: {error}");
        return archive;
    }

    static byte[] TarGz(params (string Path, string Content, UnixFileMode Mode)[] files)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        using (var writer = new TarWriter(gzip, TarEntryFormat.Gnu))
        {
            foreach (var (path, content, mode) in files)
                writer.WriteEntry(new GnuTarEntry(TarEntryType.RegularFile, path)
                {
                    Mode = mode,
                    DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content)),
                });
        }
        return output.ToArray();
    }

    /// <summary>A Debian package: an ar archive of debian-binary, control.tar.gz and data.tar.gz.</summary>
    static byte[] Deb(byte[] data, int controlLength = 4)
    {
        using var output = new MemoryStream();
        output.Write("!<arch>\n"u8);
        foreach (var (name, content) in new[] { ("debian-binary", "2.0\n"u8.ToArray()),
            ("control.tar.gz", new byte[controlLength]), ("data.tar.gz", data) })
        {
            output.Write(Encoding.ASCII.GetBytes($"{name,-16}{0,-12}{0,-6}{0,-6}{"100644",-8}{content.Length,-10}`\n"));
            output.Write(content);
            if (content.Length % 2 == 1)
                output.WriteByte((byte)'\n');
        }
        return output.ToArray();
    }
}
