using System.Formats.Tar;
using System.IO.Compression;
using SharpCompress.Writers.SevenZip;

namespace Autopilot.Core.Tests.Archives;

public enum ArchiveFormat
{
    Zip,
    TarGz,
    SevenZip,
}

/// <summary>One member of an archive a test builds; a name ending in '/' is a folder.</summary>
public sealed record TestMember(string Name, ReadOnlyMemory<byte> Data)
{
    public TestMember(string name)
        : this(name, ReadOnlyMemory<byte>.Empty)
    {
    }

    public static TestMember Text(string name, string text) => new(name, System.Text.Encoding.UTF8.GetBytes(text));

    public bool IsDirectory => Name.EndsWith('/', StringComparison.Ordinal);
}

/// <summary>Builds the archives the tests open, in each format.</summary>
public static class TestArchives
{
    public static void Write(ArchiveFormat format, string path, params TestMember[] members)
    {
        switch (format)
        {
            case ArchiveFormat.Zip:
                WriteZip(path, members);
                break;
            case ArchiveFormat.TarGz:
                WriteTarGz(path, [.. members.Select(ToTarEntry)]);
                break;
            case ArchiveFormat.SevenZip:
                WriteSevenZip(path, members);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format));
        }
    }

    public static void WriteTarGz(string path, params TarEntry[] entries)
    {
        using FileStream file = File.Create(path);
        using GZipStream gzip = new(file, CompressionLevel.Optimal);
        using TarWriter writer = new(gzip, TarEntryFormat.Pax);
        foreach (TarEntry entry in entries)
        {
            writer.WriteEntry(entry);
        }
    }

    public static TarEntry TarFile(string name, byte[] data) =>
        new PaxTarEntry(TarEntryType.RegularFile, name) { DataStream = new MemoryStream(data) };

    public static TarEntry TarFolder(string name) => new PaxTarEntry(TarEntryType.Directory, name);

    /// <summary>The header git archive writes first, holding the commit id.</summary>
    public static TarEntry TarGlobalHeader() =>
        new PaxGlobalExtendedAttributesTarEntry(new Dictionary<string, string> { ["comment"] = "0123abcd" });

    public static TarEntry TarSpecial(TarEntryType type, string name) => new PaxTarEntry(type, name);

    public static TarEntry TarLink(TarEntryType type, string name, string target) =>
        new PaxTarEntry(type, name) { LinkName = target };

    public static TestMember[] ManyFiles(int count) =>
        [.. Enumerable.Range(0, count).Select(i => TestMember.Text($"f{i}.txt", "x"))];

    /// <summary>Rewrites the uncompressed size in every local and central header of a zip, as a lying archive would.</summary>
    public static void UnderstateZipSizes(string path, uint size)
    {
        byte[] bytes = File.ReadAllBytes(path);
        ReadOnlySpan<byte> local = [0x50, 0x4B, 0x03, 0x04];
        ReadOnlySpan<byte> central = [0x50, 0x4B, 0x01, 0x02];
        for (int i = 0; i + 4 <= bytes.Length; i++)
        {
            int offset = bytes.AsSpan(i, 4).SequenceEqual(local) ? 22 : bytes.AsSpan(i, 4).SequenceEqual(central) ? 24 : -1;
            if (offset > 0)
            {
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i + offset), size);
            }
        }

        File.WriteAllBytes(path, bytes);
    }

    public static byte[] Zeros(int length) => new byte[length];

    public static byte[] Noise(int length) => System.Security.Cryptography.RandomNumberGenerator.GetBytes(length);

    private static void WriteZip(string path, TestMember[] members)
    {
        using ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (TestMember member in members)
        {
            ZipArchiveEntry entry = zip.CreateEntry(member.Name, CompressionLevel.Optimal);
            using Stream stream = entry.Open();
            stream.Write(member.Data.Span);
        }
    }

    private static PaxTarEntry ToTarEntry(TestMember member) =>
        member.IsDirectory
            ? new PaxTarEntry(TarEntryType.Directory, member.Name)
            : new PaxTarEntry(TarEntryType.RegularFile, member.Name) { DataStream = new MemoryStream(member.Data.ToArray()) };

    private static void WriteSevenZip(string path, TestMember[] members)
    {
        using FileStream file = File.Create(path);
        using SevenZipWriter writer = new(file, new SevenZipWriterOptions());
        foreach (TestMember member in members)
        {
            if (member.IsDirectory)
            {
                writer.WriteDirectory(member.Name, null);
            }
            else
            {
                using MemoryStream data = new(member.Data.ToArray());
                writer.Write(member.Name, data, null);
            }
        }
    }
}
