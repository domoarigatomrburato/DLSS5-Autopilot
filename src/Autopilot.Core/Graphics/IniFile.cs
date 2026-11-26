using System.Text;

namespace Autopilot.Core.Graphics;

/// <summary>One <c>key=value</c> line of an ini file, with the section it sits in.</summary>
internal sealed record IniEntry(string Section, string Key, string Value);

/// <summary>
/// A game's ini file, read as untrusted text: the game rewrites it and players edit it by hand,
/// so any encoding, a BOM or none, spaces around the <c>=</c>, a quoted value, a comment after it.
/// </summary>
internal static class IniFile
{
    /// <summary>A larger file is not read.</summary>
    public const int MaxBytes = 4 * 1024 * 1024;

    private const int NulProbe = 256;

    private static readonly string[] _lineEnds = ["\r\n", "\r", "\n"];

    /// <summary>The file's text, or null when it is missing, too large or unreadable.</summary>
    public static async Task<string?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            FileInfo file = new(path);
            if (!file.Exists || file.Length > MaxBytes)
            {
                return null;
            }

            return Decode(await File.ReadAllBytesAsync(path, cancellationToken));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The entries in file order; section and key names are compared ignoring case by the caller.</summary>
    public static IEnumerable<IniEntry> Entries(string text)
    {
        string section = "";
        foreach (string raw in text.Split(_lineEnds, StringSplitOptions.None))
        {
            string line = raw.Trim();
            int close = line.IndexOf(']', StringComparison.Ordinal);
            if (line.StartsWith('[', StringComparison.Ordinal) && close > 0)
            {
                section = line[1..close].Trim();
            }
            else if (!line.StartsWith(';', StringComparison.Ordinal) && line.IndexOf('=', StringComparison.Ordinal) is var equals and > 0)
            {
                yield return new IniEntry(section, line[..equals].Trim(), Value(line[(equals + 1)..]));
            }
        }
    }

    private static string Value(string raw)
    {
        int comment = raw.IndexOf(';', StringComparison.Ordinal);
        return (comment < 0 ? raw : raw[..comment]).Trim().Trim('"').Trim();
    }

    // UTF-16 with or without a BOM (a NUL early on gives it away), UTF-8 with a BOM, else Latin-1.
    private static string Decode(byte[] bytes)
    {
        if (bytes is [0xFE, 0xFF, ..])
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes is [0xFF, 0xFE, ..] || bytes.AsSpan(0, Math.Min(bytes.Length, NulProbe)).Contains((byte)0))
        {
            int start = bytes is [0xFF, 0xFE, ..] ? 2 : 0;
            return Encoding.Unicode.GetString(bytes, start, bytes.Length - start);
        }

        return bytes is [0xEF, 0xBB, 0xBF, ..] ? Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3) : Encoding.Latin1.GetString(bytes);
    }
}
