using System.Buffers.Binary;

namespace Autopilot.Core.Archives;

/// <summary>The formats the extractor opens, told apart by their first bytes, never by the file name.</summary>
internal enum ArchiveFormat
{
    Zip,
    TarGz,
    SevenZip,
}

/// <summary>An archive's format, and where it starts in its file: 0, or past the program a zip is appended to.</summary>
internal readonly record struct DetectedArchive(ArchiveFormat Format, long Start);

/// <summary>The first bytes of each format: the one table of archive signatures.</summary>
internal static class ArchiveSignature
{
    // The end-of-central-directory record: its signature, its fixed size, and how far before the
    // end of the file it is looked for, after a comment of up to 64 KiB.
    private const uint EndOfDirectorySignature = 0x06054B50;
    private const int EndOfDirectorySize = 22;
    private const int EndOfDirectorySearch = 0xFFFF + EndOfDirectorySize;

    // A zip64 locator sits just before the record it extends.
    private const uint Zip64LocatorSignature = 0x07064B50;
    private const int Zip64LocatorSize = 20;

    private static readonly byte[] _zipEntry = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] _zipDirectoryEntry = [0x50, 0x4B, 0x01, 0x02];
    private static readonly byte[] _zipEmpty = [0x50, 0x4B, 0x05, 0x06];
    private static readonly byte[] _gzip = [0x1F, 0x8B];
    private static readonly byte[] _sevenZip = [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C];
    private static readonly byte[] _program = [0x4D, 0x5A];

    /// <summary>How many first bytes tell every format apart.</summary>
    public static int Length => _sevenZip.Length;

    /// <summary>Whether <paramref name="head"/> starts a Windows program (<c>MZ</c>).</summary>
    public static bool IsProgram(ReadOnlySpan<byte> head) => head.StartsWith(_program);

    /// <summary>The format <paramref name="head"/> starts, or null when it starts none of them.</summary>
    public static ArchiveFormat? Of(ReadOnlySpan<byte> head) =>
        head.StartsWith(_zipEntry) || head.StartsWith(_zipEmpty) ? ArchiveFormat.Zip
        : head.StartsWith(_gzip) ? ArchiveFormat.TarGz
        : head.StartsWith(_sevenZip) ? ArchiveFormat.SevenZip
        : null;

    /// <summary>
    /// The archive in the file: one of the formats from its first byte, or a zip appended to a
    /// Windows program (ReShade's setup), found from the end-of-central-directory record at its end.
    /// </summary>
    /// <exception cref="ArchiveException">The file holds no archive the extractor opens.</exception>
    public static async Task<DetectedArchive> DetectAsync(string archivePath, CancellationToken ct)
    {
        await using FileStream file = File.OpenRead(archivePath);
        byte[] head = new byte[Length];
        int length = await file.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, ct);
        if (Of(head.AsSpan(0, length)) is { } format)
        {
            return new(format, 0);
        }

        return IsProgram(head.AsSpan(0, length))
            ? new(ArchiveFormat.Zip, await AppendedZipStartAsync(file, archivePath, ct))
            : throw ArchiveException.For(ArchiveFailure.NotAnArchive, archivePath, "is not a zip, tar.gz or 7z archive");
    }

    // A zip's central directory offset counts from the zip's own start, so the start is where the
    // directory sits less that offset. It must lie past the program's MZ and open a local header.
    // Some tools (zip -A, self-extractor builds) count the offsets from the file's start instead:
    // the directory then sits exactly at its offset, and the zip is read as the whole file.
    private static async Task<long> AppendedZipStartAsync(FileStream file, string archivePath, CancellationToken ct)
    {
        int tailLength = (int)Math.Min(file.Length, EndOfDirectorySearch);
        byte[] tail = new byte[tailLength];
        file.Position = file.Length - tailLength;
        await file.ReadExactlyAsync(tail, ct);
        int at = EndOfDirectoryIn(tail)
            ?? throw NotAZip(archivePath, "is a program with no zip appended");
        ReadOnlySpan<byte> record = tail.AsSpan(at, EndOfDirectorySize);
        uint directorySize = BinaryPrimitives.ReadUInt32LittleEndian(record[12..]);
        uint directoryOffset = BinaryPrimitives.ReadUInt32LittleEndian(record[16..]);
        if (IsZip64(tail, at, record))
        {
            throw NotAZip(archivePath, "is a program with a zip64 archive appended, which is not opened");
        }

        long recordPosition = file.Length - tailLength + at;
        long start = recordPosition - directorySize - directoryOffset;
        if (start < _program.Length)
        {
            return (long)directoryOffset + directorySize == recordPosition && await StartsWithAsync(file, directoryOffset, _zipDirectoryEntry, ct)
                ? 0
                : throw NotAZip(archivePath, "is a program whose appended zip points outside the file");
        }

        return await StartsWithAsync(file, start, _zipEntry, ct)
            ? start
            : throw NotAZip(archivePath, "is a program whose appended zip does not start with a file");
    }

    private static async Task<bool> StartsWithAsync(FileStream file, long position, byte[] signature, CancellationToken ct)
    {
        byte[] found = new byte[signature.Length];
        file.Position = position;
        int read = await file.ReadAtLeastAsync(found, found.Length, throwOnEndOfStream: false, ct);
        return found.AsSpan(0, read).SequenceEqual(signature);
    }

    // The last record signature whose fixed part fits, as zipfile finds it: bytes may follow the
    // zip (a signature added after appending), and a false match is refused by the checks after.
    private static int? EndOfDirectoryIn(byte[] tail)
    {
        for (int at = tail.Length - EndOfDirectorySize; at >= 0; at--)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(at)) == EndOfDirectorySignature)
            {
                return at;
            }
        }
        return null;
    }

    // Zip64 marks its size and offset as all ones and puts a locator before the record. An entry
    // count of 65535 alone is a real count.
    private static bool IsZip64(byte[] tail, int at, ReadOnlySpan<byte> record) =>
        BinaryPrimitives.ReadUInt32LittleEndian(record[12..]) == uint.MaxValue
        || BinaryPrimitives.ReadUInt32LittleEndian(record[16..]) == uint.MaxValue
        || (at >= Zip64LocatorSize && BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(at - Zip64LocatorSize)) == Zip64LocatorSignature);

    private static ArchiveException NotAZip(string archivePath, string detail) =>
        ArchiveException.For(ArchiveFailure.NotAnArchive, archivePath, detail);
}
