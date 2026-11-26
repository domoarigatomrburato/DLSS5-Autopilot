using System.Buffers.Binary;
using System.Reflection.PortableExecutable;

namespace Autopilot.Core.Executables;

/// <summary>The data directories this tool reads, by their index in the optional header.</summary>
internal enum DataDirectory
{
    Export = 0,
    Import = 1,
    Resource = 2,
    DelayImport = 13,
}

/// <summary>
/// An executable opened for ranged reads, its headers checked. Every read is a bounded range of
/// the file; nothing loads it whole, so a 500 MB executable costs no more than a small one.
/// </summary>
internal sealed class PeImage : IDisposable
{
    private const int DosHeaderSize = 0x40;
    private const int NewHeaderPointer = 0x3C;
    private const int SignatureAndMachine = 6;
    private const ushort I386 = 0x014C;
    private const ushort Amd64 = 0x8664;

    private static readonly FileStreamOptions _options = new()
    {
        Mode = FileMode.Open,
        Access = FileAccess.Read,
        Share = FileShare.ReadWrite | FileShare.Delete,
        Options = FileOptions.Asynchronous | FileOptions.RandomAccess,
    };

    private readonly FileStream _file;
    private readonly long _length;
    private readonly PEHeader _header;
    private readonly IReadOnlyList<SectionHeader> _sections;

    private PeImage(string path, FileStream file, long length, PEHeaders headers, Bitness bitness)
    {
        Path = path;
        _file = file;
        _length = length;
        _header = headers.PEHeader!;
        _sections = headers.SectionHeaders;
        Bitness = bitness;
    }

    public string Path { get; }

    public Bitness Bitness { get; }

    public ulong ImageBase => _header.ImageBase;

    /// <summary>
    /// Opens the file and checks its headers. Access denied while opening or on the first read
    /// escapes as <see cref="UnauthorizedAccessException"/>, so the caller can tell a protected
    /// executable from an unreadable one; every other failure is a <see cref="PeException"/>.
    /// </summary>
    public static async Task<PeImage> OpenAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return await OpenFileAsync(path, cancellationToken);
        }
        catch (IOException e) when (e is not PeException)
        {
            throw new PeException(PeFailure.Unreadable, path, e);
        }
    }

    public void Dispose() => _file.Dispose();

    /// <summary>The data directory's RVA, or null when the image has no such table.</summary>
    public uint? DirectoryRva(DataDirectory directory)
    {
        // A count at or below the index means the table does not exist, whatever bytes follow.
        if ((uint)_header.NumberOfRvaAndSizes <= (uint)directory)
        {
            return null;
        }

        DirectoryEntry entry = directory switch
        {
            DataDirectory.Export => _header.ExportTableDirectory,
            DataDirectory.Import => _header.ImportTableDirectory,
            DataDirectory.Resource => _header.ResourceTableDirectory,
            _ => _header.DelayImportTableDirectory,
        };
        return entry.RelativeVirtualAddress == 0 ? null : (uint)entry.RelativeVirtualAddress;
    }

    /// <summary>
    /// Up to <paramref name="count"/> bytes at an RVA (a sum of RVAs and offsets past the 32-bit
    /// range maps nowhere), cut at the end of its section's raw data:
    /// empty for an RVA outside every section's raw data, shorter at the end of the section or file.
    /// </summary>
    public async Task<byte[]> ReadRvaAsync(long rva, int count, CancellationToken cancellationToken)
    {
        foreach (SectionHeader section in _sections)
        {
            long delta = rva - (uint)section.VirtualAddress;
            long raw = (uint)section.SizeOfRawData;
            if (delta >= 0 && delta < raw)
            {
                return await ReadAtAsync((uint)section.PointerToRawData + delta, (int)Math.Min(count, raw - delta), cancellationToken);
            }
        }

        return [];
    }

    /// <summary>
    /// The printable ASCII string at an RVA, ended by a terminator within <paramref name="maxBytes"/>
    /// (terminator included); null when there is none or a byte is not printable ASCII.
    /// </summary>
    public async Task<string?> ReadAsciiAsync(long rva, int maxBytes, CancellationToken cancellationToken)
    {
        byte[] bytes = await ReadRvaAsync(rva, maxBytes, cancellationToken);
        int end = Array.IndexOf(bytes, (byte)0);
        if (end < 0 || bytes.AsSpan(0, end).ContainsAnyExceptInRange((byte)0x20, (byte)0x7E))
        {
            return null;
        }

        return System.Text.Encoding.ASCII.GetString(bytes, 0, end);
    }

    public static ushort U16(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt16LittleEndian(bytes[at..]);

    public static uint U32(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);

    public static ulong U64(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt64LittleEndian(bytes[at..]);

    private static async Task<PeImage> OpenFileAsync(string path, CancellationToken cancellationToken)
    {
        FileStream file = new(path, _options);
        try
        {
            long length = RandomAccess.GetLength(file.SafeFileHandle);
            // The protected-executable check: Windows refuses a store game's content here.
            byte[] dos = await ReadRawAsync(file, 0, DosHeaderSize, cancellationToken);
            Bitness bitness = await CheckSignaturesAsync(file, path, length, dos, cancellationToken);
            return new PeImage(path, file, length, ReadHeaders(file, path, length, bitness), bitness);
        }
        catch
        {
            await file.DisposeAsync();
            throw;
        }
    }

    // Our own checks come before the BCL reader's, so each kind of bad file gets its own error.
    private static async Task<Bitness> CheckSignaturesAsync(FileStream file, string path, long length, byte[] dos, CancellationToken cancellationToken)
    {
        if (dos.Length < DosHeaderSize || dos[0] != (byte)'M' || dos[1] != (byte)'Z')
        {
            throw new PeException(PeFailure.NotExecutable, path);
        }

        uint newHeader = U32(dos, NewHeaderPointer);
        byte[] signature = newHeader < length ? await ReadRawAsync(file, newHeader, (int)Math.Min(SignatureAndMachine, length - newHeader), cancellationToken) : [];
        if (signature.Length < SignatureAndMachine || !signature.AsSpan(0, 4).SequenceEqual("PE\0\0"u8))
        {
            throw new PeException(PeFailure.NoPeHeader, path);
        }

        ushort machine = U16(signature, 4);
        return machine switch
        {
            Amd64 => Bitness.Bits64,
            I386 => Bitness.Bits32,
            _ => throw new PeException(PeFailure.UnsupportedMachine, path, machine: machine),
        };
    }

    private static PEHeaders ReadHeaders(FileStream file, string path, long length, Bitness bitness)
    {
        PEHeaders headers;
        try
        {
            file.Position = 0;
            // The size bounds the headers' reads; the BCL refuses a stream over 2 GB without it.
            headers = new PEHeaders(file, (int)Math.Min(length, int.MaxValue));
        }
        catch (BadImageFormatException e)
        {
            throw new PeException(PeFailure.CorruptHeader, path, e);
        }

        // The loader refuses an optional header whose magic disagrees with the machine.
        PEMagic expected = bitness == Bitness.Bits64 ? PEMagic.PE32Plus : PEMagic.PE32;
        return headers.PEHeader?.Magic == expected ? headers : throw new PeException(PeFailure.CorruptHeader, path);
    }

    private async Task<byte[]> ReadAtAsync(long offset, int count, CancellationToken cancellationToken)
    {
        int wanted = (int)Math.Clamp(_length - offset, 0, count);
        try
        {
            return await ReadRawAsync(_file, offset, wanted, cancellationToken);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new PeException(PeFailure.Unreadable, Path, e);
        }
    }

    // Short only at the end of the file.
    private static async Task<byte[]> ReadRawAsync(FileStream file, long offset, int count, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[count];
        int filled = 0;
        while (filled < count)
        {
            int read = await RandomAccess.ReadAsync(file.SafeFileHandle, buffer.AsMemory(filled), offset + filled, cancellationToken);
            if (read == 0)
            {
                return buffer[..filled];
            }

            filled += read;
        }

        return buffer;
    }
}
