using Autopilot.Core.Executables;

namespace Autopilot.Core.Tests.Executables;

[TestClass]
public sealed class VersionTests(TestContext testContext) : ExecutableTestBase(testContext)
{
    [TestMethod]
    [DataRow("1.2.3.4", "1.2.3.4")]
    [DataRow("310.8.0.0", "310.8.0")]
    [DataRow("310.8.1.0", "310.8.1")]
    [DataRow("3.7.20.0", "3.7.20")]
    [DataRow("1.0.0.0", "1.0.0")]
    [DataRow("0.0.0.1", "0.0.0.1")]
    public async Task The_stamped_version_drops_only_a_fourth_part_of_zero(string stamped, string expected) => Assert.AreEqual(expected, await VersionOfAsync(new TestPe().WithVersion(new VersionBlock(stamped))));

    [TestMethod]
    public async Task A_32_bit_dll_has_its_version_read_too() => Assert.AreEqual("310.2.1", await VersionOfAsync(new TestPe(TestPe.I386).WithVersion(new VersionBlock("310.2.1"))));

    [TestMethod]
    public async Task With_two_languages_the_first_one_counts() => Assert.AreEqual("3.7.20", await VersionOfAsync(new TestPe().WithVersion(new VersionBlock("3.7.20"), new VersionBlock("9.9.9.9"))));

    [TestMethod]
    public async Task A_dll_without_a_version_resource_has_no_version() => Assert.AreEqual("", await VersionOfAsync(new TestPe().WithImports("kernel32.dll")));

    [TestMethod]
    public async Task A_fixed_block_shorter_than_52_bytes_is_no_version() => Assert.AreEqual("", await VersionOfAsync(new TestPe().WithVersion(new VersionBlock("1.2.3.4", ValueLength: 48))));

    [TestMethod]
    public async Task A_fixed_block_with_the_wrong_signature_is_no_version() => Assert.AreEqual("", await VersionOfAsync(new TestPe().WithVersion(new VersionBlock("1.2.3.4", Signature: 0xFEEF04BC))));

    [TestMethod]
    public async Task A_resource_tree_that_loops_is_no_version() => Assert.AreEqual("", await VersionOfAsync(new TestPe().WithResourceLoop()));

    [TestMethod]
    [DataRow(60)]
    [DataRow(100)]
    public async Task A_resource_tree_cut_short_is_no_version(int cut)
    {
        TestPe pe = new TestPe().WithVersion(new VersionBlock("1.2.3.4"));
        pe.EndRawData(cut);

        Assert.AreEqual("", await VersionOfAsync(pe));
    }

    [TestMethod]
    public async Task A_resource_tree_without_a_version_type_is_no_version() =>
        Assert.AreEqual("", await VersionOfAsync(new TestPe().WithResourceType(3)));

    [TestMethod]
    public async Task A_resource_directory_outside_every_section_is_no_version()
    {
        TestPe pe = new();
        pe.SetDirectory(TestPe.ResourceDirectory, 0x9_0000, 0x100);

        Assert.AreEqual("", await VersionOfAsync(pe));
    }

    [TestMethod]
    public async Task A_file_that_is_not_an_executable_has_no_version_to_read()
    {
        string path = Write("not a dll"u8.ToArray(), "nvngx_dlss.dll");

        _ = await FailsAsync(PeFailure.NotExecutable, () => Executable.FileVersionAsync(path, Ct));
    }

    private async Task<string> VersionOfAsync(TestPe pe) => await Executable.FileVersionAsync(Write(pe, "nvngx_dlss.dll"), Ct);
}
