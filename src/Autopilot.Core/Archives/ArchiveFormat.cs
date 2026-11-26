namespace Autopilot.Core.Archives;

/// <summary>The formats the extractor opens, told apart by their first bytes, never by the file name.</summary>
internal enum ArchiveFormat
{
    Zip,
    TarGz,
    SevenZip,
}

internal static class ArchiveSignature
{
    private static readonly byte[] _zipEntry = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] _zipEmpty = [0x50, 0x4B, 0x05, 0x06];
    private static readonly byte[] _gzip = [0x1F, 0x8B];
    private static readonly byte[] _sevenZip = [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C];

    public static async Task<ArchiveFormat> DetectAsync(string archivePath, CancellationToken ct)
    {
        byte[] head = new byte[_sevenZip.Length];
        int length;
        await using (FileStream file = File.OpenRead(archivePath))
        {
            length = await file.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, ct);
        }

        ReadOnlySpan<byte> start = head.AsSpan(0, length);
        if (start.StartsWith(_zipEntry) || start.StartsWith(_zipEmpty))
        {
            return ArchiveFormat.Zip;
        }

        if (start.StartsWith(_gzip))
        {
            return ArchiveFormat.TarGz;
        }

        return start.StartsWith(_sevenZip)
            ? ArchiveFormat.SevenZip
            : throw ArchiveException.For(ArchiveFailure.NotAnArchive, archivePath, "is not a zip, tar.gz or 7z archive");
    }
}
