using System.Collections.Frozen;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using Autopilot.Core.Archives;

namespace Autopilot.Core.Net;

/// <summary>
/// Whether a file starts the way its name says. A captive portal, a DNS filter or an antivirus
/// answers 200 with an HTML page, and a cut transfer can leave a zip without its directory; either
/// one must never be cached. Names with other extensions pass unchecked.
/// </summary>
internal static class FileContent
{
    private const int ShownBytes = 16;

    private static readonly byte[] _mz = "MZ"u8.ToArray();

    private static readonly FrozenDictionary<string, ArchiveFormat> _archives = new Dictionary<string, ArchiveFormat>
    {
        [".zip"] = ArchiveFormat.Zip,
        [".7z"] = ArchiveFormat.SevenZip,
        [".gz"] = ArchiveFormat.TarGz,
        [".tgz"] = ArchiveFormat.TarGz,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> _executables =
        new[] { ".exe", ".dll", ".addon64", ".addon32" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the file at <paramref name="path"/> looks like a <paramref name="fileName"/>; a zip must also open.</summary>
    public static async Task<bool> LooksRightAsync(string path, string fileName, CancellationToken cancellationToken)
    {
        string extension = Path.GetExtension(fileName);
        bool isArchive = _archives.TryGetValue(extension, out ArchiveFormat format);
        if (!isArchive && !_executables.Contains(extension))
        {
            return true;
        }
        await using FileStream file = File.OpenRead(path);
        byte[] head = new byte[ArchiveSignature.Length];
        int read = await file.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, cancellationToken);
        ReadOnlySpan<byte> start = head.AsSpan(0, read);
        if (!isArchive)
        {
            return start.StartsWith(_mz);
        }
        return ArchiveSignature.Of(start) == format
            && (format != ArchiveFormat.Zip || await ZipOpensAsync(file, cancellationToken));
    }

    /// <summary>The file's first bytes as text, printable ASCII kept and everything else escaped.</summary>
    public static string Start(string path)
    {
        using FileStream file = File.OpenRead(path);
        Span<byte> head = stackalloc byte[ShownBytes];
        int read = file.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
        StringBuilder shown = new();
        foreach (byte value in head[..read])
        {
            _ = value is >= 0x20 and < 0x7F and not (byte)'\\'
                ? shown.Append((char)value)
                : shown.Append(CultureInfo.InvariantCulture, $"\\x{value:x2}");
        }
        return shown.ToString();
    }

    // Opening reads the central directory, which a cut zip lacks.
    private static async Task<bool> ZipOpensAsync(FileStream file, CancellationToken cancellationToken)
    {
        file.Position = 0;
        try
        {
            await using ZipArchive archive = await ZipArchive.CreateAsync(file, ZipArchiveMode.Read, leaveOpen: true, entryNameEncoding: null, cancellationToken);
            return archive.Entries.Count >= 0;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }
}
