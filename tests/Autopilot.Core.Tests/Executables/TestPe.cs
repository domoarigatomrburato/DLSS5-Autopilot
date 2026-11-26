using System.Buffers.Binary;
using System.Text;

namespace Autopilot.Core.Tests.Executables;

/// <summary>One language block of a test version resource.</summary>
internal sealed record VersionBlock(string Version, uint Signature = VersionBlock.FixedInfoSignature, ushort ValueLength = 52)
{
    public const uint FixedInfoSignature = 0xFEEF04BD;
}

/// <summary>
/// Builds Windows executables for the tests, byte by byte: PE32 or PE32+, any machine, one
/// section holding every table at RVA 0x1000. High-level helpers write well-formed tables; the
/// low-level ones (<see cref="Put"/>, <see cref="Patch"/>, <see cref="EndRawData"/>) let a test
/// break them.
/// </summary>
internal sealed class TestPe(ushort machine = TestPe.Amd64)
{
    public const ushort Amd64 = 0x8664;
    public const ushort I386 = 0x014C;
    public const ushort Arm64 = 0xAA64;
    public const ushort Arm64Ec = 0xA641;
    public const int ExportDirectory = 0;
    public const int ImportDirectory = 1;
    public const int ResourceDirectory = 2;
    public const int DelayImportDirectory = 13;
    public const uint SectionRva = 0x1000;
    public const int ImportDescriptorSize = 20;
    public const int DelayDescriptorSize = 32;

    private const int HeadersSize = 0x400;
    private const int FileAlignment = 0x200;
    private const uint SubdirectoryFlag = 0x8000_0000;

    private readonly List<byte> _data = [];
    private readonly (uint Rva, uint Size)[] _directories = new (uint, uint)[16];
    private readonly List<byte[]> _appended = [];
    private int? _rawSize;

    public ushort Machine { get; } = machine;

    /// <summary>The optional header's magic, which also picks its layout; by default the machine's own.</summary>
    public ushort Magic { get; init; } = machine == I386 ? (ushort)0x10B : (ushort)0x20B;

    public uint DirectoryCount { get; init; } = 16;

    /// <summary>Where the section's bytes sit in the file; far out for a large sparse file.</summary>
    public long RawOffset { get; init; } = HeadersSize;

    /// <summary>The file's length when larger than its content, padded with a sparse hole.</summary>
    public long Size { get; init; }

    public ulong ImageBase => Is64 ? 0x1_4000_0000UL : 0x40_0000UL;

    private bool Is64 => Magic == 0x20B;

    private uint NextRva => SectionRva + (uint)((_data.Count + 3) & ~3);

    /// <summary>Places bytes in the section, 4-byte aligned, and returns their RVA.</summary>
    public uint Put(ReadOnlySpan<byte> bytes)
    {
        uint rva = NextRva;
        _data.AddRange(new byte[rva - SectionRva - _data.Count]);
        _data.AddRange(bytes);
        return rva;
    }

    public uint PutAscii(string text) => Put(Encoding.ASCII.GetBytes(text + "\0"));

    /// <summary>Overwrites four bytes already placed at <paramref name="rva"/>.</summary>
    public void Patch(uint rva, uint value)
    {
        int at = (int)(rva - SectionRva);
        byte[] bytes = U32(value);
        for (int i = 0; i < 4; i++)
        {
            _data[at + i] = bytes[i];
        }
    }

    public void SetDirectory(int index, uint rva, uint size) => _directories[index] = (rva, size);

    /// <summary>Ends the section's raw data here, less <paramref name="cut"/> bytes; what follows is in the file but outside it.</summary>
    public void EndRawData(int cut = 0) => _rawSize = _data.Count - cut;

    /// <summary>Text written after the section, as a string search would find it in a real executable.</summary>
    public TestPe Append(string text, Encoding encoding)
    {
        _appended.Add(encoding.GetBytes(text));
        return this;
    }

    public TestPe WithImports(params string[] names)
    {
        _ = ImportTable([.. names.Select(PutAscii), 0]);
        return this;
    }

    public TestPe WithDelayImports(params string[] names)
    {
        _ = DelayTable([.. names.Select(name => (1u, PutAscii(name))), (0u, 0u)]);
        return this;
    }

    /// <summary>One import descriptor per name RVA, in order; a zero RVA writes an all-zero descriptor.</summary>
    public uint ImportTable(params uint[] nameRvas)
    {
        uint table = Put(Concat(nameRvas.Select(name => name == 0 ? new byte[ImportDescriptorSize] : Concat(U32(0), U32(0), U32(0), U32(name), U32(name)))));
        SetDirectory(ImportDirectory, table, (uint)(nameRvas.Length * ImportDescriptorSize));
        return table;
    }

    /// <summary>One delay-load descriptor per entry; attributes bit 0 marks a descriptor of RVAs.</summary>
    public uint DelayTable(params (uint Attributes, uint NameRva)[] descriptors)
    {
        uint table = Put(Concat(descriptors.Select(d => Concat(U32(d.Attributes), U32(d.NameRva), new byte[DelayDescriptorSize - 8]))));
        SetDirectory(DelayImportDirectory, table, (uint)(descriptors.Length * DelayDescriptorSize));
        return table;
    }

    /// <summary>Exports variables by name, each with the bytes it holds.</summary>
    public TestPe WithExports(params (string Name, byte[] Data)[] symbols) =>
        WithExportTable([.. symbols.Select(symbol => (symbol.Name, Put(symbol.Data)))]);

    /// <summary>Exports names at the given RVAs; the counts in the directory can be overstated.</summary>
    public TestPe WithExportTable((string Name, uint Rva)[] symbols, uint? functionCount = null, uint? nameCount = null)
    {
        (string Name, uint Rva)[] sorted = [.. symbols.OrderBy(symbol => symbol.Name, StringComparer.Ordinal)];
        uint[] names = [.. sorted.Select(symbol => PutAscii(symbol.Name))];
        uint functions = Put(Concat(sorted.Select(symbol => U32(symbol.Rva))));
        uint namePointers = Put(Concat(names.Select(U32)));
        uint ordinals = Put(Concat(Enumerable.Range(0, sorted.Length).Select(i => U16((ushort)i))));
        uint count = (uint)sorted.Length;
        byte[] directory = Concat(new byte[12], U32(PutAscii("game.exe")), U32(1), U32(functionCount ?? count), U32(nameCount ?? count), U32(functions), U32(namePointers), U32(ordinals));
        SetDirectory(ExportDirectory, Put(directory), 40);
        return this;
    }

    /// <summary>A pointer, as the image's loader would see it, to <paramref name="text"/> placed in the section.</summary>
    public byte[] PointerTo(string text)
    {
        ulong address = ImageBase + PutAscii(text);
        return Is64 ? U64(address) : U32((uint)address);
    }

    /// <summary>A version resource as Windows writes it: type 16, name 1, one data entry per language.</summary>
    public TestPe WithVersion(params VersionBlock[] languages)
    {
        uint root = NextRva;
        int dataEntries = 0x48 + (8 * (languages.Length - 1));
        int blocks = dataEntries + (16 * languages.Length);
        List<byte> tree = [.. Directory(16, 0x18, SubdirectoryFlag), .. Directory(1, 0x30, SubdirectoryFlag)];
        tree.AddRange(DirectoryHeader(languages.Length));
        for (int i = 0; i < languages.Length; i++)
        {
            tree.AddRange(Concat(U32(0x409u + (uint)i), U32((uint)(dataEntries + (16 * i)))));
        }

        List<byte[]> infos = [.. languages.Select(VersionInfo)];
        int at = blocks;
        foreach (byte[] info in infos)
        {
            tree.AddRange(Concat(U32(root + (uint)at), U32((uint)info.Length), U32(0), U32(0)));
            at += (info.Length + 3) & ~3;
        }

        foreach (byte[] info in infos)
        {
            tree.AddRange(info);
            tree.AddRange(new byte[((info.Length + 3) & ~3) - info.Length]);
        }

        SetDirectory(ResourceDirectory, Put([.. tree]), (uint)tree.Count);
        return this;
    }

    /// <summary>A resource tree whose name directory points back at its root.</summary>
    public TestPe WithResourceLoop()
    {
        byte[] tree = Concat(Directory(16, 0x18, SubdirectoryFlag), Directory(1, 0, SubdirectoryFlag));
        SetDirectory(ResourceDirectory, Put(tree), (uint)tree.Length);
        return this;
    }

    /// <summary>A resource tree holding one type, other than a version, with nothing under it.</summary>
    public TestPe WithResourceType(uint type)
    {
        byte[] tree = Concat(Directory(type, 0x18, SubdirectoryFlag), DirectoryHeader(0));
        SetDirectory(ResourceDirectory, Put(tree), (uint)tree.Length);
        return this;
    }

    public byte[] ToBytes()
    {
        using MemoryStream stream = new();
        WriteTo(stream);
        return stream.ToArray();
    }

    public void WriteTo(Stream stream)
    {
        stream.Write(Headers());
        stream.Position = RawOffset;
        int raw = _rawSize ?? ((_data.Count + FileAlignment - 1) & ~(FileAlignment - 1));
        byte[] section = [.. _data, .. new byte[Math.Max(0, raw - _data.Count)]];
        stream.Write(section);
        foreach (byte[] text in _appended)
        {
            stream.Write(text);
        }

        if (Size > stream.Length)
        {
            stream.SetLength(Size);
        }
    }

    public static byte[] U16(ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return bytes;
    }

    public static byte[] U32(uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }

    public static byte[] U64(ulong value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] Concat(params byte[][] parts) => [.. parts.SelectMany(part => part)];

    private static byte[] Concat(IEnumerable<byte[]> parts) => [.. parts.SelectMany(part => part)];

    private static byte[] DirectoryHeader(int idEntries) => Concat(new byte[14], U16((ushort)idEntries));

    private static byte[] Directory(uint id, uint offset, uint flag) => Concat(DirectoryHeader(1), U32(id), U32(offset | flag));

    // The layout of tests/fake_pe.py: header, "VS_VERSION_INFO" in UTF-16, then the fixed block.
    private static byte[] VersionInfo(VersionBlock block)
    {
        uint[] parts = [.. block.Version.Split('.').Select(uint.Parse).Concat([0u, 0u, 0u, 0u]).Take(4)];
        uint ms = (parts[0] << 16) | parts[1];
        uint ls = (parts[2] << 16) | parts[3];
        uint[] fixedInfo = [block.Signature, 0x10000, ms, ls, ms, ls, 0x3F, 0, 0x40004, 2, 0, 0, 0];
        byte[] key = Encoding.Unicode.GetBytes("VS_VERSION_INFO\0");
        byte[] head = Concat(U16(0), U16(block.ValueLength), U16(0), key, new byte[2]);
        byte[] info = Concat(head, Concat(fixedInfo.Select(U32)));
        BinaryPrimitives.WriteUInt16LittleEndian(info, (ushort)info.Length);
        return info;
    }

    private byte[] Headers()
    {
        using MemoryStream stream = new(new byte[HeadersSize]);
        using BinaryWriter writer = new(stream);
        writer.Write("MZ"u8);
        stream.Position = 0x3C;
        writer.Write(0x40u);
        writer.Write("PE\0\0"u8);
        writer.Write(Machine);
        writer.Write((ushort)1);
        writer.Write(new byte[12]);
        writer.Write((ushort)(Is64 ? 240 : 224));
        writer.Write((ushort)(Is64 ? 0x0022 : 0x0102));
        WriteOptionalHeader(writer);
        WriteSectionHeader(writer);
        return stream.ToArray();
    }

    private void WriteOptionalHeader(BinaryWriter writer)
    {
        writer.Write(Magic);
        writer.Write((ushort)14);
        writer.Write(new byte[16]);
        writer.Write(SectionRva);
        if (Is64)
        {
            writer.Write(ImageBase);
        }
        else
        {
            writer.Write(SectionRva);
            writer.Write((uint)ImageBase);
        }

        writer.Write(0x1000u);
        writer.Write((uint)FileAlignment);
        writer.Write(Concat(U16(6), U16(0), U16(0), U16(0), U16(6), U16(0), U32(0)));
        writer.Write(SectionRva + (uint)((_data.Count + 0xFFF) & ~0xFFF) + 0x1000);
        writer.Write((uint)HeadersSize);
        writer.Write(0u);
        writer.Write((ushort)2);
        writer.Write((ushort)(Is64 ? 0x160 : 0x140));
        foreach (uint size in new uint[] { 0x100000, 0x1000, 0x100000, 0x1000 })
        {
            writer.Write(Is64 ? U64(size) : U32(size));
        }

        writer.Write(0u);
        writer.Write(DirectoryCount);
        foreach ((uint rva, uint size) in _directories)
        {
            writer.Write(rva);
            writer.Write(size);
        }
    }

    private void WriteSectionHeader(BinaryWriter writer)
    {
        writer.Write(".data\0\0\0"u8);
        writer.Write((uint)Math.Max(_data.Count, 1));
        writer.Write(SectionRva);
        writer.Write((uint)(_rawSize ?? ((_data.Count + FileAlignment - 1) & ~(FileAlignment - 1))));
        writer.Write((uint)RawOffset);
        writer.Write(new byte[12]);
        writer.Write(0xC000_0040u);
    }
}
