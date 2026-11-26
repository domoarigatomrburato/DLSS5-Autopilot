using Autopilot.Core.Archives;

namespace Autopilot.Core.Tests.Archives;

[TestClass]
public sealed class DamagedArchiveTests(TestContext testContext) : ArchiveTestBase(testContext)
{
    [TestMethod]
    public async Task An_html_page_saved_as_a_zip_is_not_an_archive()
    {
        string archive = In("download.zip");
        await File.WriteAllTextAsync(archive, "<!DOCTYPE html><html><body>Rate limited</body></html>", Ct);
        string destination = In("out");

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ExtractTreeAsync(archive, destination, ct: Ct));

        Assert.AreEqual(ArchiveFailure.NotAnArchive, failure.Reason);
        StringAssert.Contains(failure.Message, "download.zip", StringComparison.Ordinal);
        AssertNothingWritten(destination);
    }

    [TestMethod]
    public async Task An_empty_zip_unpacks_to_an_empty_folder()
    {
        string archive = Build(ArchiveFormat.Zip);
        string destination = In("out");

        IReadOnlyList<string> written = await ArchiveExtractor.ExtractTreeAsync(archive, destination, ct: Ct);

        Assert.IsEmpty(written);
        Assert.IsTrue(Directory.Exists(destination));
    }

    [TestMethod]
    public async Task A_zip_missing_its_central_directory_is_corrupt()
    {
        string archive = Build(ArchiveFormat.Zip, TestMember.Text("a.txt", "some text that is long enough"), TestMember.Text("b.txt", "more"));
        Truncate(archive, keep: 0.6);
        string destination = In("out");

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ExtractTreeAsync(archive, destination, ct: Ct));

        Assert.AreEqual(ArchiveFailure.Corrupt, failure.Reason);
        AssertNothingWritten(destination);
    }

    [TestMethod]
    [DataRow(ArchiveFormat.TarGz)]
    [DataRow(ArchiveFormat.SevenZip)]
    public async Task A_truncated_archive_is_corrupt_and_leaves_nothing(ArchiveFormat format)
    {
        byte[] noise = TestArchives.Noise(64 << 10);
        string archive = Build(format, new TestMember("a.bin", noise), new TestMember("b.bin", noise));
        Truncate(archive, keep: 0.7);
        string destination = In("out");

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ExtractTreeAsync(archive, destination, ct: Ct));

        Assert.AreEqual(ArchiveFailure.Corrupt, failure.Reason);
        AssertNothingWritten(destination);
    }

    [TestMethod]
    public async Task A_zip_member_whose_header_understates_its_size_writes_nothing()
    {
        string archive = Build(ArchiveFormat.Zip, new TestMember("big.bin", TestArchives.Zeros(4096)));
        TestArchives.UnderstateZipSizes(archive, 100);
        string destination = In("big.bin");

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ExtractMemberAsync(archive, "big.bin", destination, ct: Ct));

        // System.IO.Compression stops at the declared size, and the checksum then fails.
        Assert.AreEqual(ArchiveFailure.Corrupt, failure.Reason);
        AssertNothingWritten(destination);
    }

    private static void Truncate(string path, double keep)
    {
        using FileStream file = File.Open(path, FileMode.Open);
        file.SetLength((long)(file.Length * keep));
    }
}
