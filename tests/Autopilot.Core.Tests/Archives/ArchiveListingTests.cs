using System.Formats.Tar;
using Autopilot.Core.Archives;

namespace Autopilot.Core.Tests.Archives;

/// <summary>The preview reads what a package holds from the cache, writing nothing.</summary>
[TestClass]
public sealed class ArchiveListingTests(TestContext testContext) : ArchiveTestBase(testContext)
{
    public static IEnumerable<object[]> Listable => [[ArchiveFormat.Zip], [ArchiveFormat.TarGz]];

    [TestMethod]
    [DynamicData(nameof(Listable))]
    public async Task The_files_are_listed_in_archive_order_with_their_sizes_and_nothing_is_written(ArchiveFormat format)
    {
        string archive = Build(
            format,
            new TestMember("pack/"),
            TestMember.Text("pack/shaders/lumenite_core.fxh", "core"),
            new TestMember("pack/readme.txt", TestArchives.Zeros(3000)),
            TestMember.Text(@"pack\textures\noise.png", "png"));

        IReadOnlyList<ArchiveMember> members = await ArchiveExtractor.ListAsync(archive, limits: null, Ct);

        CollectionAssert.AreEqual(
            new[]
            {
                new ArchiveMember("pack/shaders/lumenite_core.fxh", 4),
                new ArchiveMember("pack/readme.txt", 3000),
                new ArchiveMember("pack/textures/noise.png", 3),
            },
            members.ToArray());
        CollectionAssert.AreEqual(new[] { archive }, Directory.GetFileSystemEntries(Work));
    }

    [TestMethod]
    public async Task A_git_archive_header_is_counted_and_not_listed()
    {
        string archive = In("snapshot.tar.gz");
        TestArchives.WriteTarGz(archive, TestArchives.TarGlobalHeader(), TestArchives.TarFolder("repo/"), TestArchives.TarFile("repo/a.fx", [1, 2]));

        IReadOnlyList<ArchiveMember> members = await ArchiveExtractor.ListAsync(archive, limits: null, Ct);

        Assert.AreEqual(new ArchiveMember("repo/a.fx", 2), members.Single());
        _ = await Assert.ThrowsExactlyAsync<ArchiveException>(() => ArchiveExtractor.ListAsync(archive, new ArchiveLimits { MaxEntries = 2 }, Ct));
    }

    [TestMethod]
    public async Task A_7z_archive_cannot_be_listed()
    {
        string archive = Build(ArchiveFormat.SevenZip, TestMember.Text("OptiScaler.dll", "dll"));

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(() => ArchiveExtractor.ListAsync(archive, limits: null, Ct));

        Assert.AreEqual(ArchiveFailure.NotListable, failure.Reason);
        CollectionAssert.AreEqual(new[] { archive }, Directory.GetFileSystemEntries(Work));
    }

    public static IEnumerable<object[]> Refusals()
    {
        foreach (ArchiveFormat format in new[] { ArchiveFormat.Zip, ArchiveFormat.TarGz })
        {
            yield return [format, new[] { TestMember.Text("../escape.dll", "x") }, null!, ArchiveFailure.UnsafeName];
            yield return [format, new[] { TestMember.Text("CON.txt", "x") }, null!, ArchiveFailure.UnsafeName];
            yield return [format, new[] { TestMember.Text("a.dll", "x"), TestMember.Text("A.DLL", "y") }, null!, ArchiveFailure.DuplicateName];
            yield return [format, TestArchives.ManyFiles(6), new ArchiveLimits { MaxEntries = 5 }, ArchiveFailure.TooManyEntries];
            yield return [format, new[] { new TestMember("a.bin", new byte[600]), new TestMember("b.bin", new byte[600]) }, new ArchiveLimits { MaxTotalBytes = 1000 }, ArchiveFailure.TooLarge];
            yield return [format, new[] { new TestMember("zeros.bin", TestArchives.Zeros(2 << 20)) }, new ArchiveLimits { MaxRatio = 10 }, ArchiveFailure.RatioExceeded];
        }
    }

    [TestMethod]
    [DynamicData(nameof(Refusals))]
    public async Task A_listing_refuses_what_unpacking_refuses(ArchiveFormat format, TestMember[] members, ArchiveLimits? limits, ArchiveFailure reason)
    {
        string archive = Build(format, members);

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(() => ArchiveExtractor.ListAsync(archive, limits, Ct));

        Assert.AreEqual(reason, failure.Reason);
        CollectionAssert.AreEqual(new[] { archive }, Directory.GetFileSystemEntries(Work));
    }

    [TestMethod]
    [DataRow(TarEntryType.SymbolicLink)]
    [DataRow(TarEntryType.HardLink)]
    public async Task A_listing_refuses_a_tar_link(TarEntryType type)
    {
        string archive = In("links.tar.gz");
        TestArchives.WriteTarGz(archive, TestArchives.TarFile("a.txt", [1]), TestArchives.TarLink(type, "b.txt", "a.txt"));

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(() => ArchiveExtractor.ListAsync(archive, limits: null, Ct));

        Assert.AreEqual(ArchiveFailure.Link, failure.Reason);
        Assert.AreEqual("b.txt", failure.Member);
    }

    [TestMethod]
    public async Task A_page_saved_as_a_zip_is_not_an_archive_and_a_cut_zip_is_corrupt()
    {
        string page = In("download.zip");
        await File.WriteAllTextAsync(page, "<!DOCTYPE html><html></html>", Ct);
        string cut = Build(ArchiveFormat.Zip, TestMember.Text("a.txt", "some text that is long enough"), TestMember.Text("b.txt", "more"));
        await using (FileStream file = File.Open(cut, FileMode.Open))
        {
            file.SetLength(file.Length * 6 / 10);
        }

        ArchiveException notAnArchive = await Assert.ThrowsExactlyAsync<ArchiveException>(() => ArchiveExtractor.ListAsync(page, limits: null, Ct));
        ArchiveException corrupt = await Assert.ThrowsExactlyAsync<ArchiveException>(() => ArchiveExtractor.ListAsync(cut, limits: null, Ct));

        Assert.AreEqual(ArchiveFailure.NotAnArchive, notAnArchive.Reason);
        Assert.AreEqual(ArchiveFailure.Corrupt, corrupt.Reason);
    }
}
