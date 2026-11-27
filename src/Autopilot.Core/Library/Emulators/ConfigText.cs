using System.Xml;
using System.Xml.Linq;
using Autopilot.Core.Graphics;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Library.Emulators;

/// <summary>
/// Reads one setting out of an emulator's config file, as untrusted text: capped in size, any
/// encoding with a BOM, either line ending, comments. Only the one key is read; nothing is parsed
/// whole, so a file the emulator itself would refuse still gives what that key says.
/// </summary>
internal static class ConfigText
{
    /// <summary>A larger file is not an emulator's config.</summary>
    public const int MaxBytes = 1024 * 1024;

    private static readonly string[] _lineEnds = ["\r\n", "\r", "\n"];

    /// <summary>The file's bytes; null when it is missing, too large or cannot be read.</summary>
    public static async Task<byte[]?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            FileInfo file = new(path);
            return file.Exists && file.Length <= MaxBytes ? await File.ReadAllBytesAsync(path, cancellationToken) : null;
        }
        catch (Exception e) when (DrivePaths.IsNotThere(e))
        {
            return null;
        }
    }

    /// <summary>The lines of a text file, decoded from its BOM.</summary>
    public static string[] Lines(byte[] bytes) => IniFile.Decode(bytes).Split(_lineEnds, StringSplitOptions.None);

    /// <summary>
    /// The value of <paramref name="key"/> in an ini, TOML or cfg file: inside <c>[section]</c>, or
    /// anywhere for a file without sections (<paramref name="section"/> null). The first one counts.
    /// </summary>
    public static string? KeyValue(byte[] bytes, string? section, string key)
    {
        bool inside = section is null;
        foreach (string raw in Lines(bytes))
        {
            string line = raw.Trim();
            if (section is not null && line.StartsWith('[', StringComparison.Ordinal) && line.EndsWith(']', StringComparison.Ordinal))
            {
                inside = line[1..^1].Trim().Equals(section, StringComparison.OrdinalIgnoreCase);
            }
            else if (inside && Assigned(line, '=', key) is { } value)
            {
                return Clean(value, "#;");
            }
        }

        return null;
    }

    /// <summary>The value of <c>key:</c> inside a top-level YAML block (<c>Video:</c>), read line by line.</summary>
    public static string? YamlValue(byte[] bytes, string block, string key)
    {
        bool inside = false;
        foreach (string raw in Lines(bytes))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#', StringComparison.Ordinal))
            {
                continue;
            }

            if (!char.IsWhiteSpace(raw[0]))
            {
                inside = line.Equals(block + ":", StringComparison.Ordinal);
            }
            else if (inside && Assigned(line, ':', key) is { } value)
            {
                return Clean(value, "#");
            }
        }

        return null;
    }

    /// <summary>The text of the element at <paramref name="path"/> below the root, in an XML file read with no DTD and no external entities.</summary>
    public static string? XmlValue(byte[] bytes, params string[] path)
    {
        XmlReaderSettings settings = new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxBytes };
        try
        {
            using MemoryStream stream = new(bytes);
            using var reader = XmlReader.Create(stream, settings);
            XElement? element = XDocument.Load(reader).Root;
            foreach (string name in path)
            {
                element = element?.Element(name);
            }

            return element?.Value.Trim();
        }
        catch (XmlException)
        {
            return null;
        }
    }

    // "key = value" (or "key: value"), the key in any case; null when the line sets another key.
    private static string? Assigned(string line, char separator, string key)
    {
        int at = line.IndexOf(separator, StringComparison.Ordinal);
        return at > 0 && line[..at].Trim().Equals(key, StringComparison.OrdinalIgnoreCase) ? line[(at + 1)..] : null;
    }

    // A quoted value is what is between the quotes; a bare one ends where a comment starts.
    private static string Clean(string value, string commentStarts)
    {
        string text = value.Trim();
        if (text.Length > 0 && text[0] is '"' or '\'' && text.IndexOf(text[0], 1, StringComparison.Ordinal) is var close and > 0)
        {
            return text[1..close];
        }

        int comment = text.IndexOfAny(commentStarts.ToCharArray());
        return (comment < 0 ? text : text[..comment]).Trim();
    }
}
