using Autopilot.Core.Archives;

namespace Autopilot.Core.Tests.Archives;

[TestClass]
public sealed class ArchiveSelectionTests(TestContext testContext) : ArchiveTestBase(testContext)
{
    [TestMethod]
    [DynamicData(nameof(Formats))]
    public async Task A_normal_archive_unpacks_its_whole_tree(ArchiveFormat format)
    {
        string archive = Build(
            format,
            new TestMember("Pack/"),
            new TestMember("Pack/Shaders/"),
            TestMember.Text("Pack/Shaders/a.fx", "shader a"),
            TestMember.Text("Pack/Textures/b.png", "texture b"),
            TestMember.Text("Pack/readme.txt", "read me"));
        string destination = In("out");

        IReadOnlyList<string> written = await ArchiveExtractor.ExtractTreeAsync(archive, destination, ct: Ct);

        CollectionAssert.AreEquivalent(
            new[] { Path.Join(destination, "Pack", "Shaders", "a.fx"), Path.Join(destination, "Pack", "Textures", "b.png"), Path.Join(destination, "Pack", "readme.txt") },
            written.ToArray());
        Assert.AreEqual("shader a", await File.ReadAllTextAsync(Path.Join(destination, "Pack", "Shaders", "a.fx"), Ct));
        Assert.AreEqual("texture b", await File.ReadAllTextAsync(Path.Join(destination, "Pack", "Textures", "b.png"), Ct));
        AssertNoPart(destination);
    }

    [TestMethod]
    [DynamicData(nameof(Formats))]
    public async Task One_member_is_found_by_its_ending_on_whole_path_parts(ArchiveFormat format)
    {
        string archive = Build(
            format,
            TestMember.Text("pkg/bin/nvngx_dlss.dll", "upscaler"),
            TestMember.Text("pkg/bin/dlss.dll", "plain"));
        string destination = In("picked.dll");

        string written = await ArchiveExtractor.ExtractMemberAsync(archive, "BIN/DLSS.DLL", destination, ct: Ct);

        Assert.AreEqual(destination, written);
        Assert.AreEqual("plain", await File.ReadAllTextAsync(destination, Ct));
        AssertNoPart(destination);
    }

    [TestMethod]
    [DynamicData(nameof(Formats))]
    public async Task An_ending_that_matches_two_members_is_refused(ArchiveFormat format)
    {
        string archive = Build(format, TestMember.Text("x64/nvngx.dll", "64"), TestMember.Text("x86/nvngx.dll", "86"));
        string destination = In("nvngx.dll");

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ExtractMemberAsync(archive, "nvngx.dll", destination, ct: Ct));

        Assert.AreEqual(ArchiveFailure.AmbiguousMember, failure.Reason);
        AssertNothingWritten(destination);
    }

    [TestMethod]
    [DynamicData(nameof(Formats))]
    public async Task A_missing_member_is_named_in_the_failure(ArchiveFormat format)
    {
        string archive = Build(format, TestMember.Text("pkg/nvngx_dlss.dll", "x"));
        string destination = In("dlss.dll");

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ExtractMemberAsync(archive, "dlss.dll", destination, ct: Ct));

        Assert.AreEqual(ArchiveFailure.MemberNotFound, failure.Reason);
        StringAssert.Contains(failure.Message, "'dlss.dll'", StringComparison.Ordinal);
        AssertNothingWritten(destination);
    }

    [TestMethod]
    [DynamicData(nameof(Formats))]
    public async Task A_tree_can_drop_its_top_folder_and_keep_one_subfolder_filtered(ArchiveFormat format)
    {
        string archive = Build(
            format,
            TestMember.Text("LumeniteFX-main/Shaders/a.fx", "a"),
            TestMember.Text("LumeniteFX-main/Shaders/sub/b.FX", "b"),
            TestMember.Text("LumeniteFX-main/Shaders/c.fxh", "c"),
            TestMember.Text("LumeniteFX-main/Textures/t.png", "t"),
            TestMember.Text("top.fx", "top"));
        string destination = In("reshade-shaders");
        TreeOptions options = new() { StripTopFolder = true, SourceFolder = "shaders", Extensions = [".fx"] };

        IReadOnlyList<string> written = await ArchiveExtractor.ExtractTreeAsync(archive, destination, options, ct: Ct);

        CollectionAssert.AreEquivalent(
            new[] { Path.Join(destination, "a.fx"), Path.Join(destination, "sub", "b.FX") },
            written.ToArray());
    }

    [TestMethod]
    [DynamicData(nameof(Formats))]
    public async Task A_tree_can_keep_named_files_only(ArchiveFormat format)
    {
        string archive = Build(
            format,
            TestMember.Text("pack/a.fx", "a"),
            TestMember.Text("pack/b.fx", "b"),
            TestMember.Text("c.fx", "c"));
        string destination = In("out");
        TreeOptions options = new() { StripTopFolder = true, Names = ["B.FX", "c.fx"], Extensions = [".fx"] };

        IReadOnlyList<string> written = await ArchiveExtractor.ExtractTreeAsync(archive, destination, options, ct: Ct);

        CollectionAssert.AreEquivalent(new[] { Path.Join(destination, "b.fx"), Path.Join(destination, "c.fx") }, written.ToArray());
    }

    [TestMethod]
    public async Task Two_members_that_land_on_one_file_after_flattening_are_refused()
    {
        string archive = Build(ArchiveFormat.Zip, TestMember.Text("a/x.fx", "a"), TestMember.Text("b/x.fx", "b"));
        string destination = In("out");

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ExtractTreeAsync(archive, destination, new TreeOptions { StripTopFolder = true }, ct: Ct));

        Assert.AreEqual(ArchiveFailure.DuplicateName, failure.Reason);
        AssertNothingWritten(destination);
    }
}
