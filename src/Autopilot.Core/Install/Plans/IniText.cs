using System.Text;

namespace Autopilot.Core.Install.Plans;

/// <summary>
/// The config files an install writes into (<c>ReShade.ini</c>, and the bridge's, OptiScaler's and the feeder's), edited
/// line by line: a key set, or set only when absent, and every other line, comment, section and key order kept byte
/// for byte, with the file's own line endings (CRLF for a new file). Hand-written because no .NET or NuGet package edits
/// INI text while keeping it: <c>Microsoft.Extensions.Configuration.Ini</c> only reads.
/// </summary>
internal sealed class IniText
{
    private const string NewFileEnding = "\r\n";
    private static readonly UTF8Encoding _strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    // The byte-order marks a config file may start with, and the encoding each one says.
    private static readonly (byte[] Mark, Encoding Encoding)[] _marks =
    [
        ([0xEF, 0xBB, 0xBF], _strict),
        ([0xFF, 0xFE], new UnicodeEncoding(bigEndian: false, byteOrderMark: false)),
        ([0xFE, 0xFF], new UnicodeEncoding(bigEndian: true, byteOrderMark: false)),
    ];

    private readonly List<Line> _lines;
    private readonly string _ending;
    private readonly byte[] _mark;
    private readonly Encoding _encoding;

    private IniText(List<Line> lines, string ending, byte[] mark, Encoding encoding)
    {
        _lines = lines;
        _ending = ending;
        _mark = mark;
        _encoding = encoding;
    }

    /// <summary>
    /// The file whose bytes are <paramref name="bytes"/>; a new, empty one when null. UTF-8 with or without its mark, or UTF-16
    /// by its mark; bytes that are not UTF-8 are read one character per byte, so they are written back as they were.
    /// </summary>
    public static IniText Parse(ReadOnlyMemory<byte>? bytes)
    {
        if (bytes is not { } content)
        {
            return new([], NewFileEnding, [], _strict);
        }

        ReadOnlySpan<byte> span = content.Span;
        (byte[] mark, Encoding encoding) = Array.Find(_marks, known => content.Span.StartsWith(known.Mark)) is { Mark: not null } found ? found : ([], _strict);
        ReadOnlySpan<byte> body = span[mark.Length..];
        string text;
        try
        {
            text = encoding.GetString(body);
        }
        catch (DecoderFallbackException)
        {
            encoding = Encoding.Latin1;
            text = encoding.GetString(body);
        }

        List<Line> lines = Split(text);
        return new(lines, lines.Select(line => line.Ending).FirstOrDefault(ending => ending.Length > 0) ?? NewFileEnding, mark, encoding);
    }

    /// <summary>Sets <paramref name="key"/> in <paramref name="section"/> to <paramref name="value"/>: the first such line rewritten, else one added at the section's end, the section added at the file's end when absent.</summary>
    public IniText Set(string section, string key, string value)
    {
        if (Find(section, key) is { } at)
        {
            string written = KeyOf(_lines[at].Text);
            _lines[at] = _lines[at] with { Text = $"{written}={value}" };
            return this;
        }

        return Add(section, key, value);
    }

    /// <summary>Sets <paramref name="key"/> only when the section does not have it: a value the person chose stays.</summary>
    public IniText SetIfAbsent(string section, string key, string value) => Find(section, key) is null ? Add(section, key, value) : this;

    /// <summary>The file's bytes, in its own encoding, with its mark when it had one.</summary>
    public byte[] ToBytes() => [.. _mark, .. _encoding.GetBytes(string.Concat(_lines.Select(line => line.Text + line.Ending)))];

    private IniText Add(string section, string key, string value)
    {
        int? header = Header(section);
        if (header is null)
        {
            EndLastLine();
            if (_lines.Count > 0 && _lines[^1].Text.Trim().Length > 0)
            {
                _lines.Add(new("", _ending));
            }

            _lines.Add(new($"[{section}]", _ending));
            _lines.Add(new($"{key}={value}", _ending));
            return this;
        }

        // After the section's last line that is not blank, before the blank lines that set it off from the next.
        int end = SectionEnd(header.Value);
        int at = end;
        while (at > header.Value + 1 && _lines[at - 1].Text.Trim().Length == 0)
        {
            at--;
        }

        if (at == _lines.Count)
        {
            EndLastLine();
        }

        _lines.Insert(at, new($"{key}={value}", _ending));
        return this;
    }

    // A line added after the last one needs that one to end first.
    private void EndLastLine()
    {
        if (_lines.Count > 0 && _lines[^1].Ending.Length == 0)
        {
            _lines[^1] = _lines[^1] with { Ending = _ending };
        }
    }

    private int? Find(string section, string key)
    {
        if (Header(section) is not { } header)
        {
            return null;
        }

        for (int at = header + 1; at < SectionEnd(header); at++)
        {
            if (IsKey(_lines[at].Text) && string.Equals(KeyOf(_lines[at].Text).Trim(), key, StringComparison.OrdinalIgnoreCase))
            {
                return at;
            }
        }

        return null;
    }

    private int? Header(string section)
    {
        for (int at = 0; at < _lines.Count; at++)
        {
            if (SectionOf(_lines[at].Text) is { } name && string.Equals(name, section, StringComparison.OrdinalIgnoreCase))
            {
                return at;
            }
        }

        return null;
    }

    // The index after the section's last line: the next header, or the file's end.
    private int SectionEnd(int header)
    {
        int at = header + 1;
        while (at < _lines.Count && SectionOf(_lines[at].Text) is null)
        {
            at++;
        }

        return at;
    }

    private static string? SectionOf(string text)
    {
        string trimmed = text.Trim();
        return trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[^1] == ']' ? trimmed[1..^1].Trim() : null;
    }

    private static bool IsKey(string text)
    {
        string trimmed = text.TrimStart();
        return trimmed.Length > 0 && trimmed[0] is not (';' or '#') && trimmed.Contains('=', StringComparison.Ordinal);
    }

    private static string KeyOf(string text) => text[..text.IndexOf('=', StringComparison.Ordinal)];

    // Lines with their own endings: CRLF, LF or CR; the last may have none.
    private static List<Line> Split(string text)
    {
        List<Line> lines = [];
        int start = 0;
        for (int at = 0; at < text.Length; at++)
        {
            if (text[at] is not ('\r' or '\n'))
            {
                continue;
            }

            int length = text[at] == '\r' && at + 1 < text.Length && text[at + 1] == '\n' ? 2 : 1;
            lines.Add(new(text[start..at], text.Substring(at, length)));
            at += length - 1;
            start = at + 1;
        }

        if (start < text.Length)
        {
            lines.Add(new(text[start..], ""));
        }

        return lines;
    }

    private sealed record Line(string Text, string Ending);
}
