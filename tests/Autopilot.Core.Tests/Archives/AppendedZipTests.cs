using System.Buffers.Binary;
using Autopilot.Core.Archives;

namespace Autopilot.Core.Tests.Archives;

/// <summary>ReShade's setup: a Windows program with the add-on files in a zip appended to it.</summary>
[TestClass]
public sealed class AppendedZipTests(TestContext testContext) : ArchiveTestBase(testContext)
{
    private const int EndRecordSize = 22;

    private static readonly TestMember[] _reShade =
    [
        TestMember.Text("ReShade64.dll", "MZ 64-bit runtime"),
        TestMember.Text("ReShade32.dll", "MZ 32-bit runtime"),
        TestMember.Text("ReShade64.json", /*lang=json,strict*/ "{\"layer\": 64}"),
        TestMember.Text("ReShade32.json", /*lang=json,strict*/ "{\"layer\": 32}"),
    ];

    [TestMethod]
    public async Task One_runtime_is_unpacked_from_the_setup()
    {
        string setup = Setup(_reShade);
        string destination = In("ReShade64.dll");

        _ = await ArchiveExtractor.ExtractMemberAsync(setup, "ReShade64.dll", destination, limits: null, Ct);

        Assert.AreEqual("MZ 64-bit runtime", await File.ReadAllTextAsync(destination, Ct));
        AssertNoPart(destination);
    }

    [TestMethod]
    public async Task The_whole_package_is_unpacked_from_the_setup()
    {
        string setup = Setup(_reShade);
        string destination = In("reshade");

        IReadOnlyList<string> written = await ArchiveExtractor.ExtractTreeAsync(setup, destination, options: null, limits: null, Ct);

        CollectionAssert.AreEquivalent(_reShade.Select(member => Path.Join(destination, member.Name)).ToArray(), written.ToArray());
        Assert.AreEqual(/*lang=json,strict*/ "{\"layer\": 32}", await File.ReadAllTextAsync(Path.Join(destination, "ReShade32.json"), Ct));
    }

    [TestMethod]
    public async Task The_setups_files_are_listed_without_writing_anything()
    {
        string setup = Setup(_reShade);

        IReadOnlyList<ArchiveMember> members = await ArchiveExtractor.ListAsync(setup, limits: null, Ct);

        CollectionAssert.AreEqual(_reShade.Select(member => new ArchiveMember(member.Name, member.Data.Length)).ToArray(), members.ToArray());
        CollectionAssert.AreEqual(new[] { setup }, Directory.GetFileSystemEntries(Work));
    }

    [TestMethod]
    public async Task The_setups_files_pass_the_same_checks_as_any_zip()
    {
        string setup = Setup(TestMember.Text("ReShade64.dll", "a"), TestMember.Text("reshade64.DLL", "b"));

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ExtractTreeAsync(setup, In("out"), options: null, limits: null, Ct));

        Assert.AreEqual(ArchiveFailure.DuplicateName, failure.Reason);
        AssertNothingWritten(In("out"));
    }

    [TestMethod]
    public async Task A_program_with_no_zip_is_not_an_archive()
    {
        string program = In("game.exe");
        await File.WriteAllBytesAsync(program, Program(4096), Ct);

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ExtractTreeAsync(program, In("out"), options: null, limits: null, Ct));

        Assert.AreEqual(ArchiveFailure.NotAnArchive, failure.Reason);
        AssertNothingWritten(In("out"));
    }

    [TestMethod]
    [DataRow(-100_000L, DisplayName = "before the file's start")]
    [DataRow(1L, DisplayName = "into the program's MZ")]
    [DataRow(16L, DisplayName = "into the program, not at a file")]
    public async Task A_program_whose_zip_points_elsewhere_is_refused_as_an_archive(long start)
    {
        string setup = Setup(_reShade);
        byte[] bytes = await File.ReadAllBytesAsync(setup, Ct);
        // The zip starts where the record sits less the directory's size and offset: this offset puts it at start.
        long offset = bytes.Length - EndRecordSize - BinaryPrimitives.ReadUInt32LittleEndian(Eocd(bytes)[12..]) - start;
        BinaryPrimitives.WriteUInt32LittleEndian(Eocd(bytes)[16..], (uint)offset);
        await File.WriteAllBytesAsync(setup, bytes, Ct);

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ExtractTreeAsync(setup, In("out"), options: null, limits: null, Ct));

        Assert.AreEqual(ArchiveFailure.NotAnArchive, failure.Reason);
        AssertNothingWritten(In("out"));
    }

    [TestMethod]
    public async Task A_program_whose_zip_is_damaged_is_corrupt()
    {
        string setup = Setup(_reShade);
        byte[] bytes = await File.ReadAllBytesAsync(setup, Ct);
        uint directorySize = BinaryPrimitives.ReadUInt32LittleEndian(Eocd(bytes)[12..]);
        // The central directory's first entry loses its signature.
        bytes[bytes.Length - EndRecordSize - (int)directorySize] = 0;
        await File.WriteAllBytesAsync(setup, bytes, Ct);

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ExtractTreeAsync(setup, In("out"), options: null, limits: null, Ct));

        Assert.AreEqual(ArchiveFailure.Corrupt, failure.Reason);
        AssertNothingWritten(In("out"));
    }

    [TestMethod]
    public async Task A_program_with_a_zip64_appended_is_refused_as_such()
    {
        string setup = Setup(_reShade);
        byte[] bytes = await File.ReadAllBytesAsync(setup, Ct);
        BinaryPrimitives.WriteUInt32LittleEndian(Eocd(bytes)[16..], uint.MaxValue);
        await File.WriteAllBytesAsync(setup, bytes, Ct);

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ListAsync(setup, limits: null, Ct));

        Assert.AreEqual(ArchiveFailure.NotAnArchive, failure.Reason);
        StringAssert.Contains(failure.Message, "zip64", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task An_entry_count_of_65535_alone_is_not_taken_for_zip64()
    {
        string setup = Setup(_reShade);
        byte[] bytes = await File.ReadAllBytesAsync(setup, Ct);
        BinaryPrimitives.WriteUInt16LittleEndian(Eocd(bytes)[10..], ushort.MaxValue);
        await File.WriteAllBytesAsync(setup, bytes, Ct);

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ListAsync(setup, limits: null, Ct));

        Assert.AreEqual(ArchiveFailure.Corrupt, failure.Reason, "the zip is opened, and its count does not match its directory");
    }

    [TestMethod]
    public async Task A_setup_with_bytes_after_its_zip_still_opens()
    {
        string setup = Setup(_reShade);
        await File.WriteAllBytesAsync(setup, [.. await File.ReadAllBytesAsync(setup, Ct), .. TestArchives.Noise(4096)], Ct);

        IReadOnlyList<ArchiveMember> members = await ArchiveExtractor.ListAsync(setup, limits: null, Ct);
        _ = await ArchiveExtractor.ExtractMemberAsync(setup, "ReShade32.dll", In("ReShade32.dll"), limits: null, Ct);

        Assert.HasCount(_reShade.Length, members);
        Assert.AreEqual("MZ 32-bit runtime", await File.ReadAllTextAsync(In("ReShade32.dll"), Ct));
    }

    [TestMethod]
    public async Task A_setup_whose_offsets_count_from_the_files_start_opens()
    {
        string setup = AbsoluteSetup(_reShade);
        string destination = In("reshade");

        IReadOnlyList<ArchiveMember> members = await ArchiveExtractor.ListAsync(setup, limits: null, Ct);
        _ = await ArchiveExtractor.ExtractTreeAsync(setup, destination, options: null, limits: null, Ct);

        CollectionAssert.AreEqual(_reShade.Select(member => new ArchiveMember(member.Name, member.Data.Length)).ToArray(), members.ToArray());
        Assert.AreEqual("MZ 64-bit runtime", await File.ReadAllTextAsync(Path.Join(destination, "ReShade64.dll"), Ct));
    }

    [TestMethod]
    public async Task A_setup_whose_file_relative_directory_offset_is_wrong_is_refused()
    {
        string setup = AbsoluteSetup(_reShade);
        byte[] bytes = await File.ReadAllBytesAsync(setup, Ct);
        uint offset = BinaryPrimitives.ReadUInt32LittleEndian(Eocd(bytes)[16..]);
        BinaryPrimitives.WriteUInt32LittleEndian(Eocd(bytes)[16..], offset + 1);
        await File.WriteAllBytesAsync(setup, bytes, Ct);

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ListAsync(setup, limits: null, Ct));

        Assert.AreEqual(ArchiveFailure.NotAnArchive, failure.Reason);
    }

    [TestMethod]
    public async Task A_program_with_a_zip64_locator_is_refused_as_such()
    {
        byte[] zip = await File.ReadAllBytesAsync(Build(ArchiveFormat.Zip, _reShade), Ct);
        byte[] locator = new byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(locator, 0x07064B50);
        string setup = In("setup.exe");
        await File.WriteAllBytesAsync(setup, [.. Program(2048), .. zip[..^EndRecordSize], .. locator, .. zip[^EndRecordSize..]], Ct);

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ListAsync(setup, limits: null, Ct));

        StringAssert.Contains(failure.Message, "zip64", StringComparison.Ordinal);
    }

    // An MZ stub of a few KB, then a zip made with ZipArchive, as ReShade's setup is built.
    private string Setup(params TestMember[] members)
    {
        string setup = In("ReShade_Setup_6.5.1_Addon.exe");
        string zip = In("addon.zip");
        TestArchives.Write(ArchiveFormat.Zip, zip, members);
        File.WriteAllBytes(setup, [.. Program(4096), .. File.ReadAllBytes(zip)]);
        File.Delete(zip);
        return setup;
    }

    // The same setup as zip -A leaves it: every offset counts from the file's start, stub included.
    private string AbsoluteSetup(params TestMember[] members)
    {
        string setup = Setup(members);
        byte[] bytes = File.ReadAllBytes(setup);
        uint stub = (uint)(bytes.Length - EndRecordSize
            - BinaryPrimitives.ReadUInt32LittleEndian(Eocd(bytes)[12..]) - BinaryPrimitives.ReadUInt32LittleEndian(Eocd(bytes)[16..]));
        int at = (int)(stub + BinaryPrimitives.ReadUInt32LittleEndian(Eocd(bytes)[16..]));
        for (int entry = 0; entry < members.Length; entry++)
        {
            Span<byte> header = bytes.AsSpan(at);
            BinaryPrimitives.WriteUInt32LittleEndian(header[42..], BinaryPrimitives.ReadUInt32LittleEndian(header[42..]) + stub);
            at += 46 + BinaryPrimitives.ReadUInt16LittleEndian(header[28..]) + BinaryPrimitives.ReadUInt16LittleEndian(header[30..]) + BinaryPrimitives.ReadUInt16LittleEndian(header[32..]);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(Eocd(bytes)[16..], BinaryPrimitives.ReadUInt32LittleEndian(Eocd(bytes)[16..]) + stub);
        File.WriteAllBytes(setup, bytes);
        return setup;
    }

    // The end-of-central-directory record, with no comment: entries at [10..], the directory's size
    // at [12..] and its offset from the zip's start at [16..].
    private static Span<byte> Eocd(byte[] bytes) => bytes.AsSpan(bytes.Length - EndRecordSize);

    private static byte[] Program(int length)
    {
        byte[] program = TestArchives.Noise(length);
        program[0] = (byte)'M';
        program[1] = (byte)'Z';
        return program;
    }
}
