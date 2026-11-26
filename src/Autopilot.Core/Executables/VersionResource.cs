using System.Globalization;

namespace Autopilot.Core.Executables;

/// <summary>
/// The file version stamped in an executable's <c>VS_FIXEDFILEINFO</c>, read from its resource
/// tree: type 16 (RT_VERSION), its first name, its first language. The tree is untrusted: it is
/// walked exactly three levels deep, with bounded entry counts and checked offsets.
/// </summary>
internal static class VersionResource
{
    /// <summary>Entries read at most from one resource directory.</summary>
    public const int MaxEntries = 4096;

    /// <summary>Bytes of the <c>VS_VERSIONINFO</c> block read: its header, key and fixed block fit well within.</summary>
    public const int MaxBlockBytes = 1024;

    private const uint VersionType = 16;
    private const uint HighBit = 0x8000_0000;
    private const int DirectoryHeaderSize = 16;
    private const int EntrySize = 8;
    private const int DataEntrySize = 16;
    private const int BlockHeaderSize = 6;
    private const int FixedInfoSize = 52;
    private const uint FixedInfoSignature = 0xFEEF04BD;

    public static async Task<string> ReadAsync(PeImage image, CancellationToken cancellationToken)
    {
        if (image.DirectoryRva(DataDirectory.Resource) is not { } root)
        {
            return "";
        }

        uint? names = await SubdirectoryAsync(image, root, 0, entry => entry.Id == VersionType, cancellationToken);
        uint? languages = names is { } n ? await SubdirectoryAsync(image, root, n, _ => true, cancellationToken) : null;
        Entry? leaf = languages is { } l ? await FirstEntryAsync(image, root, l, _ => true, cancellationToken) : null;
        if (leaf is not { IsDirectory: false } data)
        {
            return "";
        }

        byte[] block = await BlockAsync(image, root, data.Offset, cancellationToken);
        return FixedFileVersion(block);
    }

    private static async Task<uint?> SubdirectoryAsync(PeImage image, uint root, uint directory, Func<Entry, bool> wanted, CancellationToken cancellationToken)
    {
        Entry? entry = await FirstEntryAsync(image, root, directory, wanted, cancellationToken);
        return entry is { IsDirectory: true } ? entry.Offset : null;
    }

    private static async Task<Entry?> FirstEntryAsync(PeImage image, uint root, uint directory, Func<Entry, bool> wanted, CancellationToken cancellationToken)
    {
        long at = (long)root + directory;
        byte[] header = await image.ReadRvaAsync(at, DirectoryHeaderSize, cancellationToken);
        if (header.Length < DirectoryHeaderSize)
        {
            return null;
        }

        int count = Math.Min(PeImage.U16(header, 12) + PeImage.U16(header, 14), MaxEntries);
        byte[] entries = await image.ReadRvaAsync(at + DirectoryHeaderSize, count * EntrySize, cancellationToken);
        for (int i = 0; i + EntrySize <= entries.Length; i += EntrySize)
        {
            Entry entry = new(PeImage.U32(entries, i), PeImage.U32(entries, i + 4));
            if (wanted(entry))
            {
                return entry;
            }
        }

        return null;
    }

    private static async Task<byte[]> BlockAsync(PeImage image, uint root, uint dataEntry, CancellationToken cancellationToken)
    {
        byte[] entry = await image.ReadRvaAsync((long)root + dataEntry, DataEntrySize, cancellationToken);
        if (entry.Length < DataEntrySize)
        {
            return [];
        }

        // The data entry holds an RVA, not an offset in the tree.
        int size = (int)Math.Min(PeImage.U32(entry, 4), MaxBlockBytes);
        return await image.ReadRvaAsync(PeImage.U32(entry, 0), size, cancellationToken);
    }

    // VS_VERSIONINFO: length, value length and type (2 bytes each), the key in UTF-16 with its
    // terminator, padding to 4 bytes, then the VS_FIXEDFILEINFO as the value.
    private static string FixedFileVersion(byte[] block)
    {
        int keyEnd = KeyEnd(block);
        if (keyEnd < 0 || PeImage.U16(block, 2) < FixedInfoSize)
        {
            return "";
        }

        int value = (keyEnd + 3) & ~3;
        if (value + FixedInfoSize > block.Length || PeImage.U32(block, value) != FixedInfoSignature)
        {
            return "";
        }

        uint ms = PeImage.U32(block, value + 8);
        uint ls = PeImage.U32(block, value + 12);
        uint[] parts = [ms >> 16, ms & 0xFFFF, ls >> 16, ls & 0xFFFF];
        int shown = parts[3] == 0 ? 3 : 4;
        return string.Join('.', parts.Take(shown).Select(part => part.ToString(CultureInfo.InvariantCulture)));
    }

    // The offset just past the key's terminator, or -1 when the block ends first.
    private static int KeyEnd(byte[] block)
    {
        for (int at = BlockHeaderSize; at + 2 <= block.Length; at += 2)
        {
            if (block[at] == 0 && block[at + 1] == 0)
            {
                return at + 2;
            }
        }

        return -1;
    }

    private sealed record Entry(uint Name, uint Target)
    {
        // A named entry has the high bit set in its name; its id is meaningless.
        public uint? Id => (Name & HighBit) == 0 ? Name : null;

        public bool IsDirectory => (Target & HighBit) != 0;

        public uint Offset => Target & ~HighBit;
    }
}
