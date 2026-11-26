namespace Autopilot.Core.Archives;

/// <summary>The formats the extractor opens, told apart by their first bytes, never by the file name.</summary>
internal enum ArchiveFormat
{
    Zip,
    TarGz,
    SevenZip,
}

/// <summary>The first bytes of each format: the one table of archive signatures.</summary>
internal static class ArchiveSignature
{
    private static readonly byte[] _zipEntry = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] _zipEmpty = [0x50, 0x4B, 0x05, 0x06];
    private static readonly byte[] _gzip = [0x1F, 0x8B];
    private static readonly byte[] _sevenZip = [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C];

    /// <summary>How many first bytes tell every format apart.</summary>
    public static int Length => _sevenZip.Length;

    /// <summary>The format <paramref name="head"/> starts, or null when it starts none of them.</summary>
    public static ArchiveFormat? Of(ReadOnlySpan<byte> head) =>
        head.StartsWith(_zipEntry) || head.StartsWith(_zipEmpty) ? ArchiveFormat.Zip
        : head.StartsWith(_gzip) ? ArchiveFormat.TarGz
        : head.StartsWith(_sevenZip) ? ArchiveFormat.SevenZip
        : null;

    public static async Task<ArchiveFormat> DetectAsync(string archivePath, CancellationToken ct)
    {
        byte[] head = new byte[Length];
        int length;
        await using (FileStream file = File.OpenRead(archivePath))
        {
            length = await file.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, ct);
        }

        return Of(head.AsSpan(0, length))
            ?? throw ArchiveException.For(ArchiveFailure.NotAnArchive, archivePath, "is not a zip, tar.gz or 7z archive");
    }
}
