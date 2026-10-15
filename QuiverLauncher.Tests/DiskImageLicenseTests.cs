using System.Buffers.Binary;
using System.Text;
using FluentAssertions;
using QuiverLauncher.Core.Services;

namespace QuiverLauncher.Tests;

public sealed class DiskImageLicenseTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "quiver-dmg-license-" + Guid.NewGuid().ToString("N"));

    public DiskImageLicenseTests() => Directory.CreateDirectory(_root);
    public void Dispose() => TestFixtures.CleanupDirectory(_root);

    [Fact]
    public void The_english_agreement_is_read_from_the_image()
    {
        var image = DiskImage(License(
            ("TEXT", "Français", "Licence MIT"),
            ("TEXT", "English", "MIT License\rCopyright (c) 2026\r\rPermission is hereby granted")));

        DiskImageLicense.Read(image).Should().Be("MIT License\nCopyright (c) 2026\n\nPermission is hereby granted");
    }

    [Fact]
    public void An_agreement_with_formatting_is_read_as_text()
    {
        var image = DiskImage(License(("RTF ", "English",
            @"{\rtf1\ansi{\fonttbl\f0\fswiss Helvetica;}\f0\pard Terms of use\par Don\'92t redistribute.}")));

        DiskImageLicense.Read(image).Should().Be("Terms of use\nDon’t redistribute.");
    }

    [Fact]
    public void An_image_without_an_agreement_has_none()
    {
        DiskImageLicense.Read(DiskImage("<dict><key>blkx</key><array/></dict>")).Should().BeNull();
        var notAnImage = Path.Combine(_root, "Game.dmg");
        File.WriteAllText(notAnImage, "<html>Not found</html>");
        DiskImageLicense.Read(notAnImage).Should().BeNull();
    }

    [Fact]
    public void Hdiutil_deprecation_notes_are_left_out_of_its_errors()
    {
        GameInstallationService.HdiutilProblem(
                "hdiutil: WARNING: 'hdiutil attach -nobrowse -readonly -mountpoint ...' is deprecated. Please use " +
                "'diskutil image attach --mountOptions nobrowse --readOnly --mountPoint ...' instead.\nhdiutil: attach canceled\n")
            .Should().Be("hdiutil: attach canceled");
    }

    static string License(params (string Type, string Language, string Text)[] texts)
    {
        var resources = new StringBuilder("<dict><key>LPic</key><array><dict><key>Data</key><data>AAAAAQ==</data></dict></array>");
        foreach (var group in texts.GroupBy(text => text.Type))
        {
            resources.Append($"<key>{group.Key}</key><array>");
            foreach (var (_, language, text) in group)
                resources.Append($"<dict><key>Data</key><data>{Convert.ToBase64String(Encoding.Latin1.GetBytes(text))}</data>" +
                    $"<key>Name</key><string>{language}</string></dict>");
            resources.Append("</array>");
        }
        return resources.Append("</dict>").ToString();
    }

    /// <summary>A UDIF image: some data, then its property list, then the 512-byte "koly" block pointing at it.</summary>
    string DiskImage(string resourceFork)
    {
        var propertyList = Encoding.UTF8.GetBytes("""<?xml version="1.0" encoding="UTF-8"?>""" +
            """<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">""" +
            $"""<plist version="1.0"><dict><key>resource-fork</key>{resourceFork}</dict></plist>""");
        var data = new byte[1000];
        var trailer = new byte[512];
        "koly"u8.CopyTo(trailer);
        BinaryPrimitives.WriteUInt64BigEndian(trailer.AsSpan(0xD8), (ulong)data.Length);
        BinaryPrimitives.WriteUInt64BigEndian(trailer.AsSpan(0xE0), (ulong)propertyList.Length);
        var path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".dmg");
        File.WriteAllBytes(path, [.. data, .. propertyList, .. trailer]);
        return path;
    }
}
