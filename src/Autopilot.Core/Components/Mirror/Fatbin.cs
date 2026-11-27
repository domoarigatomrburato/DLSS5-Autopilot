using System.Collections.Frozen;
using System.IO.MemoryMappedFiles;

namespace Autopilot.Core.Components.Mirror;

/// <summary>
/// The CUDA architectures (sm) a DLL carries code for, read from its fatbin records. The file is
/// memory-mapped and read in bounded slices, never whole: a <c>nvngx_dlssnr.dll</c> is about 165 MB.
/// </summary>
/// <remarks>
/// A fatbin starts with the magic <c>0xBA55ED50</c>; the u16 at +6 is its header size and the u64 at +8
/// its payload size. Its entries have a header size (u32 at +4, 24 to 4096) and a payload size (u64
/// at +8); the sm is the u32 at +24, +28 or +20 of the entry, the first of them that is a known sm.
/// </remarks>
internal static class Fatbin
{
    private const int SliceSize = 1 << 20;
    private const int MinHeaderSize = 16;
    private const int MinEntryHeader = 24;
    private const int MaxEntryHeader = 4096;
    private const int EntryTail = 32;

    private static readonly byte[] _magic = [0x50, 0xED, 0x55, 0xBA];

    private static readonly int[] _smOffsets = [24, 28, 20];

    /// <summary>Every sm the fatbin table knows.</summary>
    public static FrozenSet<int> KnownSm { get; } = new[] { 50, 52, 53, 60, 61, 62, 70, 72, 75, 80, 86, 87, 89, 90, 100, 101, 120, 121 }.ToFrozenSet();

    /// <summary>The sm the file carries code for; empty when it has no readable fatbin or cannot be read.</summary>
    public static IReadOnlySet<int> Architectures(string path)
    {
        try
        {
            using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            long length = file.Length;
            if (length < MinHeaderSize)
            {
                return FrozenSet<int>.Empty;
            }

            using var map = MemoryMappedFile.CreateFromFile(file, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: true);
            using MemoryMappedViewAccessor view = map.CreateViewAccessor(0, length, MemoryMappedFileAccess.Read);
            HashSet<int> found = [];
            foreach (long at in MagicOffsets(view, length))
            {
                ReadFatbin(new(view, length), at, found);
            }

            return found;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return FrozenSet<int>.Empty;
        }
    }

    // Slices overlap by the magic's length less one, so a magic across a slice boundary is found once.
    private static List<long> MagicOffsets(MemoryMappedViewAccessor view, long length)
    {
        List<long> offsets = [];
        byte[] slice = new byte[SliceSize];
        for (long start = 0; start < length; start += SliceSize - (_magic.Length - 1))
        {
            int count = (int)Math.Min(SliceSize, length - start);
            _ = view.ReadArray(start, slice, 0, count);
            for (int from = 0; from < count;)
            {
                int at = slice.AsSpan(from, count - from).IndexOf(_magic);
                if (at < 0)
                {
                    break;
                }

                offsets.Add(start + from + at);
                from += at + 1;
            }

            if (start + count >= length)
            {
                break;
            }
        }

        return offsets;
    }

    private static void ReadFatbin(Mapped file, long at, HashSet<int> found)
    {
        if (file.U16(at + 6) is not { } header || file.U64(at + 8) is not { } size
            || header < MinHeaderSize || size == 0 || size > (ulong)file.Length)
        {
            return;
        }

        long entry = at + header;
        long end = entry + (long)size;
        while (entry < end - EntryTail && EntrySize(file, entry) is { } entrySize)
        {
            if (SmOf(file, entry) is { } sm)
            {
                _ = found.Add(sm);
            }

            entry += entrySize;
        }
    }

    // An entry's header and payload together; null when either size is out of range, which ends the fatbin.
    private static long? EntrySize(Mapped file, long entry) =>
        file.U32(entry + 4) is { } header && file.U64(entry + 8) is { } payload
        && header is >= MinEntryHeader and <= MaxEntryHeader && payload > 0 && payload <= (ulong)file.Length
            ? header + (long)payload
            : null;

    private static int? SmOf(Mapped file, long entry) =>
        _smOffsets
            .Select(offset => file.U32(entry + offset))
            .Where(value => value is { } sm && KnownSm.Contains((int)sm))
            .Select(value => (int?)value)
            .FirstOrDefault();

    /// <summary>Little-endian reads that are null past the end of the file.</summary>
    private readonly record struct Mapped(MemoryMappedViewAccessor View, long Length)
    {
        public ushort? U16(long at) => Fits(at, sizeof(ushort)) ? View.ReadUInt16(at) : null;

        public uint? U32(long at) => Fits(at, sizeof(uint)) ? View.ReadUInt32(at) : null;

        public ulong? U64(long at) => Fits(at, sizeof(ulong)) ? View.ReadUInt64(at) : null;

        private bool Fits(long at, int size) => at >= 0 && at <= Length - size;
    }
}
