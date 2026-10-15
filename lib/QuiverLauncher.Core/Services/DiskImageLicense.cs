using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace QuiverLauncher.Core.Services;

/// <summary>
/// The license agreement a macOS disk image shows before it opens. hdiutil shows it only in a terminal, so Quiver
/// reads it from the image itself: a UDIF image ends in a "koly" block that points at a property list, whose
/// resource fork holds the agreement as TEXT (or RTF) resources, one per language.
/// </summary>
public static class DiskImageLicense
{
    const int TrailerSize = 512;
    const int MaxPropertyListSize = 64 * 1024 * 1024;

    /// <summary>The agreement's text, in English when the image has several languages; null when it has none.</summary>
    public static string? Read(string diskImagePath)
    {
        try
        {
            using var stream = File.OpenRead(diskImagePath);
            if (stream.Length < TrailerSize)
                return null;
            var trailer = new byte[TrailerSize];
            stream.Seek(-TrailerSize, SeekOrigin.End);
            stream.ReadExactly(trailer);
            if (!trailer.AsSpan(0, 4).SequenceEqual("koly"u8))
                return null;

            var offset = BinaryPrimitives.ReadUInt64BigEndian(trailer.AsSpan(0xD8));
            var length = BinaryPrimitives.ReadUInt64BigEndian(trailer.AsSpan(0xE0));
            if (length == 0 || length > MaxPropertyListSize || offset > (ulong)stream.Length - length)
                return null;
            var propertyList = new byte[length];
            stream.Seek((long)offset, SeekOrigin.Begin);
            stream.ReadExactly(propertyList);
            return FromPropertyList(propertyList);
        }
        catch (Exception ex) when (ex is IOException or XmlException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    internal static string? FromPropertyList(byte[] propertyList)
    {
        using var reader = XmlReader.Create(new MemoryStream(propertyList),
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null });
        var resources = Value(XDocument.Load(reader).Root?.Element("dict"), "resource-fork");
        // LPic marks an agreement; its text is a TEXT resource, or RTF when it has formatting.
        if (Value(resources, "LPic") == null)
            return null;

        foreach (var (type, plain) in new[] { ("TEXT", true), ("RTF ", false) })
        {
            var texts = Value(resources, type)?.Elements("dict").ToList();
            if (texts is not { Count: > 0 })
                continue;
            var english = texts.FirstOrDefault(text =>
                Value(text, "Name")?.Value.Contains("English", StringComparison.OrdinalIgnoreCase) == true) ?? texts[0];
            if (Value(english, "Data") is not { } data)
                continue;
            var bytes = Convert.FromBase64String(Regex.Replace(data.Value, @"\s", ""));
            var text = plain ? MacRoman(bytes) : FromRtf(MacRoman(bytes));
            text = text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            if (text.Length > 0)
                return text;
        }

        return null;
    }

    /// <summary>The element after the &lt;key&gt; named <paramref name="key"/> in a property-list dictionary.</summary>
    static XElement? Value(XElement? dictionary, string key) =>
        dictionary?.Elements("key").FirstOrDefault(element => element.Value == key)?.ElementsAfterSelf().FirstOrDefault();

    static string MacRoman(byte[] bytes)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try { return Encoding.GetEncoding(10000).GetString(bytes); }
        catch (ArgumentException) { return Encoding.Latin1.GetString(bytes); }
    }

    /// <summary>RTF as plain text: paragraphs kept, groups such as font tables and control words dropped.</summary>
    static string FromRtf(string rtf)
    {
        var text = Regex.Replace(rtf, @"\{\\(?:fonttbl|colortbl|stylesheet|info|\*)[^{}]*(?:\{[^{}]*\}[^{}]*)*\}", "");
        text = Regex.Replace(text, @"\\(?:par|line)\b ?", "\n");
        var codePage = Encoding.GetEncoding(rtf.Contains(@"\mac", StringComparison.Ordinal) ? 10000 : 1252);
        text = Regex.Replace(text, @"\\'([0-9a-fA-F]{2})", match => codePage.GetString([Convert.ToByte(match.Groups[1].Value, 16)]));
        text = Regex.Replace(text, @"\\[a-zA-Z]+-?\d* ?", "");
        return text.Replace("{", "").Replace("}", "").Replace("\\\\", "\\");
    }
}

/// <summary>The player didn't accept the license agreement a download asks for, so nothing was installed.</summary>
public sealed class LicenseDeclinedException(string fileName)
    : OperationCanceledException($"The license agreement for {fileName} wasn't accepted, so it wasn't installed.");
