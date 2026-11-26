using System.Formats.Tar;
using Autopilot.Core.Archives;

namespace Autopilot.Core.Tests.Archives;

[TestClass]
public sealed class ArchiveNameTests(TestContext testContext) : ArchiveTestBase(testContext)
{
    // The 7z writer strips a leading slash or "C:/" from a name, so those cases exist only in zip and tar.
    private static readonly string[] _namesEveryFormatCarries =
        ["../evil.dll", "a/../../evil.dll", "C:evil.dll", "a/x.dll:stream", "CON", "a/aux.txt", "evil.dll.", "evil.dll "];

    private static readonly string[] _absoluteNames =
        ["/etc/evil.dll", "C:/Windows/evil.dll", @"C:\Windows\evil.dll", @"\\server\share\evil.dll"];

    public static IEnumerable<object[]> UnsafeNames =>
        Formats.SelectMany(format => _namesEveryFormatCarries.Select(name => new object[] { format[0], name }))
            .Concat(_absoluteNames.SelectMany(name => new[] { new object[] { ArchiveFormat.Zip, name }, [ArchiveFormat.TarGz, name] }));

    public static IEnumerable<object[]> FileAndFolderClashes =>
        Formats.SelectMany(format => new[]
        {
            new object[] { format[0], "bin", "bin/x.dll" },
            [format[0], "Bin/x.dll", "BIN"],
        });

    [TestMethod]
    [DynamicData(nameof(FileAndFolderClashes))]
    public async Task A_file_whose_path_is_also_a_folder_is_refused(ArchiveFormat format, string first, string second)
    {
        string archive = Build(format, new TestMember(first), new TestMember(second));
        string destination = In("out");

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ExtractTreeAsync(archive, destination, ct: Ct));

        Assert.AreEqual(ArchiveFailure.DuplicateName, failure.Reason);
        AssertNothingWritten(destination);
    }

    [TestMethod]
    [DynamicData(nameof(UnsafeNames))]
    public async Task An_archive_with_one_unsafe_name_writes_nothing(ArchiveFormat format, string name)
    {
        string archive = Build(format, TestMember.Text("good.txt", "fine"), TestMember.Text(name, "evil"));
        string destination = In("deep", "out");

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ExtractTreeAsync(archive, destination, ct: Ct));

        Assert.AreEqual(ArchiveFailure.UnsafeName, failure.Reason);
        Assert.IsInstanceOfType<UnsafePathException>(failure.InnerException);
        AssertNothingWritten(destination);
        AssertNoFileNamed("evil.dll");
    }

    [TestMethod]
    [DynamicData(nameof(Formats))]
    public async Task An_unsafe_name_refuses_the_archive_even_when_it_is_not_the_member_asked_for(ArchiveFormat format)
    {
        string archive = Build(format, TestMember.Text("bin/x.dll", "wanted"), TestMember.Text("../evil.dll", "evil"));
        string destination = In("x.dll");

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ExtractMemberAsync(archive, "x.dll", destination, ct: Ct));

        Assert.AreEqual(ArchiveFailure.UnsafeName, failure.Reason);
        AssertNothingWritten(destination);
    }

    [TestMethod]
    [DynamicData(nameof(Formats))]
    public async Task Two_members_whose_names_differ_only_in_case_are_refused(ArchiveFormat format)
    {
        string archive = Build(format, TestMember.Text("Bin/X.dll", "one"), TestMember.Text("bin/x.DLL", "two"));
        string destination = In("out");

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ExtractTreeAsync(archive, destination, ct: Ct));

        Assert.AreEqual(ArchiveFailure.DuplicateName, failure.Reason);
        AssertNothingWritten(destination);
    }

    [TestMethod]
    [DataRow(TarEntryType.SymbolicLink)]
    [DataRow(TarEntryType.HardLink)]
    public async Task A_tar_with_a_link_writes_nothing(TarEntryType type)
    {
        string archive = In("links.tar.gz");
        TestArchives.WriteTarGz(archive, TestArchives.TarFile("a.txt", [1, 2, 3]), TestArchives.TarLink(type, "b.txt", "a.txt"));
        string destination = In("out");

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ExtractTreeAsync(archive, destination, ct: Ct));

        Assert.AreEqual(ArchiveFailure.Link, failure.Reason);
        AssertNothingWritten(destination);
    }

    [TestMethod]
    public async Task A_tar_with_a_device_entry_writes_nothing()
    {
        string archive = In("fifo.tar.gz");
        TestArchives.WriteTarGz(archive, TestArchives.TarFile("a.txt", [1]), TestArchives.TarSpecial(TarEntryType.Fifo, "pipe"));
        string destination = In("out");

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ExtractTreeAsync(archive, destination, ct: Ct));

        Assert.AreEqual(ArchiveFailure.UnsupportedEntry, failure.Reason);
        AssertNothingWritten(destination);
    }

    [TestMethod]
    public async Task A_tarball_made_by_git_archive_unpacks_past_its_global_header()
    {
        string archive = In("codeload.tar.gz");
        TestArchives.WriteTarGz(
            archive,
            TestArchives.TarGlobalHeader(),
            TestArchives.TarFolder("repo-main/"),
            TestArchives.TarFile("repo-main/x.txt", [1]));
        string destination = In("out");

        IReadOnlyList<string> written = await ArchiveExtractor.ExtractTreeAsync(archive, destination, ct: Ct);

        CollectionAssert.AreEqual(new[] { Path.Join(destination, "repo-main", "x.txt") }, written.ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(Formats))]
    public async Task A_folder_entry_for_the_archive_root_is_ignored(ArchiveFormat format)
    {
        string archive = Build(format, new TestMember("./"), TestMember.Text("./x.txt", "x"));
        string destination = In("out");

        IReadOnlyList<string> written = await ArchiveExtractor.ExtractTreeAsync(archive, destination, ct: Ct);

        CollectionAssert.AreEqual(new[] { Path.Join(destination, "x.txt") }, written.ToArray());
    }
}
