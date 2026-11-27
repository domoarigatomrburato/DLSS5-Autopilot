using System.Buffers.Binary;
using Autopilot.Core.Components;

namespace Autopilot.Core.Tests.Components;

[TestClass]
public sealed class FatbinTests
{
    private const int FatbinHeaderSize = 16;
    private const int EntryHeaderSize = 64;
    private const int EntryPayloadSize = 16;

    private static readonly int[] _rtxArchitectures = [75, 86, 89, 120];

    private readonly List<string> _files = [];

    [TestCleanup]
    public void DeleteFiles()
    {
        foreach (string file in _files)
        {
            File.Delete(file);
        }
    }

    [TestMethod]
    [DataRow(20)]
    [DataRow(24)]
    [DataRow(28)]
    public void Every_architecture_of_a_fatbin_is_read(int smOffset)
    {
        string file = Write(FatbinBytes(smOffset, _rtxArchitectures));

        Assert.AreEqual("75, 86, 89, 120", Architectures(file));
    }

    [TestMethod]
    public void A_fatbin_in_the_middle_of_a_larger_file_is_found()
    {
        string file = Write([.. Filler(40_000), .. FatbinBytes(24, [89]), .. Filler(30_000)]);

        Assert.AreEqual("89", Architectures(file));
    }

    [TestMethod]
    public void A_fatbin_across_the_reads_boundary_is_found_once()
    {
        int boundary = 1 << 20;
        string file = Write([.. Filler(boundary - 2), .. FatbinBytes(28, [86, 120]), .. Filler(boundary)]);

        Assert.AreEqual("86, 120", Architectures(file));
    }

    [TestMethod]
    public void Several_fatbins_add_up()
    {
        string file = Write([.. FatbinBytes(24, [75]), .. Filler(100), .. FatbinBytes(20, [120])]);

        Assert.AreEqual("75, 120", Architectures(file));
    }

    [TestMethod]
    public void A_fatbin_whose_size_is_past_the_file_is_skipped()
    {
        byte[] fatbin = FatbinBytes(24, _rtxArchitectures);
        BinaryPrimitives.WriteUInt64LittleEndian(fatbin.AsSpan(8), 1UL << 40);

        Assert.AreEqual("", Architectures(Write(fatbin)));
    }

    [TestMethod]
    public void An_entry_header_out_of_range_ends_its_fatbin()
    {
        byte[] fatbin = FatbinBytes(24, _rtxArchitectures);
        BinaryPrimitives.WriteUInt32LittleEndian(fatbin.AsSpan(FatbinHeaderSize + 4), 8);

        Assert.AreEqual("", Architectures(Write(fatbin)));
    }

    [TestMethod]
    public void A_number_that_is_no_known_architecture_is_not_one() =>
        Assert.AreEqual("", Architectures(Write(FatbinBytes(24, [77]))));

    [TestMethod]
    public void A_file_without_the_magic_has_no_architectures() =>
        Assert.AreEqual("", Architectures(Write(Filler(70_000))));

    [TestMethod]
    public void An_empty_or_missing_file_has_no_architectures()
    {
        Assert.AreEqual("", Architectures(Write([])));
        Assert.AreEqual("", Architectures(Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dll")));
    }

    /// <summary>
    /// One fatbin: the magic, a 16-byte header with the payload's size, then one entry per sm with a
    /// 64-byte header (its size at +4, its payload's at +8, the sm at <paramref name="smOffset"/>).
    /// </summary>
    internal static byte[] FatbinBytes(int smOffset, IEnumerable<int> architectures)
    {
        byte[][] entries = [.. architectures.Select(sm => Entry(smOffset, sm))];
        byte[] header = new byte[FatbinHeaderSize];
        BinaryPrimitives.WriteUInt32LittleEndian(header, 0xBA55ED50);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(6), FatbinHeaderSize);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(8), (ulong)entries.Sum(entry => entry.Length));
        return [.. header, .. entries.SelectMany(entry => entry)];
    }

    private static byte[] Entry(int smOffset, int sm)
    {
        byte[] entry = new byte[EntryHeaderSize + EntryPayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(entry, 2);
        BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(4), EntryHeaderSize);
        BinaryPrimitives.WriteUInt64LittleEndian(entry.AsSpan(8), EntryPayloadSize);
        BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(smOffset), (uint)sm);
        return entry;
    }

    private static byte[] Filler(int length) => [.. Enumerable.Repeat((byte)0xCC, length)];

    // The architectures found, in order.
    private static string Architectures(string path) => string.Join(", ", Fatbin.Architectures(path).Order());

    private string Write(byte[] content)
    {
        string path = Path.Join(Path.GetTempPath(), "autopilot-fatbin-" + Guid.NewGuid().ToString("N") + ".dll");
        File.WriteAllBytes(path, content);
        _files.Add(path);
        return path;
    }
}
