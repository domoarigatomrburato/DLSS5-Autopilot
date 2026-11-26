using Autopilot.Core.Archives;

namespace Autopilot.Core.Tests.Archives;

[TestClass]
public sealed class ArchiveDestinationTests(TestContext testContext) : ArchiveTestBase(testContext)
{
    [TestMethod]
    public async Task A_destination_reached_through_a_link_comes_back_in_the_callers_spelling()
    {
        string real = In("real-library");
        string link = LinkTo(real, In("linked-library"));
        string archive = Build(ArchiveFormat.Zip, TestMember.Text("bin/x.dll", "x"));
        string destination = Path.Join(link, "Game", "out");

        IReadOnlyList<string> written = await ArchiveExtractor.ExtractTreeAsync(archive, destination, options: null, limits: null, ct: Ct);

        CollectionAssert.AreEqual(new[] { Path.Join(link, "Game", "out", "bin", "x.dll") }, written.ToArray());
        Assert.IsTrue(File.Exists(Path.Join(real, "Game", "out", "bin", "x.dll")));
    }

    [TestMethod]
    public async Task An_existing_destination_is_left_alone()
    {
        string archive = Build(ArchiveFormat.Zip, TestMember.Text("x.dll", "new"));
        string destination = In("x.dll");
        await File.WriteAllTextAsync(destination, "old", Ct);

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ExtractMemberAsync(archive, "x.dll", destination, limits: null, ct: Ct));

        Assert.AreEqual(ArchiveFailure.DestinationExists, failure.Reason);
        Assert.AreEqual("old", await File.ReadAllTextAsync(destination, Ct));
    }

    [TestMethod]
    public async Task A_folder_part_left_by_an_earlier_crash_is_replaced()
    {
        string archive = Build(ArchiveFormat.TarGz, TestMember.Text("a/x.txt", "fresh"));
        string destination = In("out");
        TestFiles.Plant(Path.Join(destination + ".part", "stale", "old.txt"), "old");

        IReadOnlyList<string> written = await ArchiveExtractor.ExtractTreeAsync(archive, destination, options: null, limits: null, ct: Ct);

        CollectionAssert.AreEqual(new[] { Path.Join(destination, "a", "x.txt") }, written.ToArray());
        Assert.IsFalse(Directory.Exists(Path.Join(destination, "stale")));
        AssertNoPart(destination);
    }

    [TestMethod]
    public async Task A_file_part_left_by_an_earlier_crash_is_replaced()
    {
        string archive = Build(ArchiveFormat.SevenZip, TestMember.Text("x.dll", "fresh"));
        string destination = In("x.dll");
        await File.WriteAllTextAsync(destination + ".part", "stale and longer", Ct);

        _ = await ArchiveExtractor.ExtractMemberAsync(archive, "x.dll", destination, limits: null, ct: Ct);

        Assert.AreEqual("fresh", await File.ReadAllTextAsync(destination, Ct));
        AssertNoPart(destination);
    }

    [TestMethod]
    [DynamicData(nameof(Formats))]
    public async Task A_cancelled_extraction_leaves_nothing(ArchiveFormat format)
    {
        string archive = Build(format, TestMember.Text("a/x.txt", "x"));
        string destination = In("out");
        using CancellationTokenSource cancel = new();
        await cancel.CancelAsync();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(
            () => ArchiveExtractor.ExtractTreeAsync(archive, destination, options: null, limits: null, ct: cancel.Token));

        AssertNothingWritten(destination);
    }

    [TestMethod]
    public async Task An_unsafe_source_folder_leaves_nothing()
    {
        string archive = Build(ArchiveFormat.Zip, TestMember.Text("a/x.txt", "x"));
        string destination = In("out");

        _ = await Assert.ThrowsExactlyAsync<UnsafePathException>(
            () => ArchiveExtractor.ExtractTreeAsync(archive, destination, new TreeOptions { SourceFolder = "../a" }, limits: null, ct: Ct));

        AssertNothingWritten(destination);
    }
}
