using Autopilot.Core.Archives;

namespace Autopilot.Core.Tests.Archives;

[TestClass]
public sealed class SafePathTests
{
    private static readonly string _root = Path.Join("games", "Some Game");

    [TestMethod]
    [DataRow("..")]
    [DataRow("../game-other/x.dll")]
    [DataRow("a/../../x.dll")]
    [DataRow(@"a\..\x.dll")]
    [DataRow("C:x.dll")]
    [DataRow("C:/Windows/x.dll")]
    [DataRow(@"C:\Windows\x.dll")]
    [DataRow(@"\\server\share\x.dll")]
    [DataRow("/etc/x.dll")]
    [DataRow(@"\x.dll")]
    [DataRow("x.dll:stream")]
    [DataRow("a:b/x.dll")]
    [DataRow("CON")]
    [DataRow("con.txt")]
    [DataRow("a/aux.txt")]
    [DataRow("PRN")]
    [DataRow("nul.tar.gz")]
    [DataRow("COM1")]
    [DataRow("com9.log")]
    [DataRow("LPT1")]
    [DataRow("lpt9")]
    [DataRow("COM¹")]
    [DataRow("LPT³.txt")]
    [DataRow("CON .txt")]
    [DataRow("x.dll.")]
    [DataRow("x.dll ")]
    [DataRow("folder./x.dll")]
    [DataRow("folder /x.dll")]
    [DataRow("")]
    [DataRow("/")]
    [DataRow("./.")]
    [DataRow("a\tb.dll")]
    [DataRow("a<b.dll")]
    [DataRow("a>b.dll")]
    [DataRow("a\"b.dll")]
    [DataRow("a|b.dll")]
    [DataRow("a?b.dll")]
    [DataRow("a*b.dll")]
    public void A_name_Windows_would_read_outside_the_folder_or_as_a_device_is_refused(string name)
    {
        UnsafePathException failure = Assert.ThrowsExactly<UnsafePathException>(() => SafePath.Join(_root, name));

        Assert.AreEqual(name, failure.Name);
    }

    [TestMethod]
    [DataRow("x.dll", "x.dll")]
    [DataRow("a/b/x.dll", "a/b/x.dll")]
    [DataRow(@"a\b\x.dll", "a/b/x.dll")]
    [DataRow("a//b/./x.dll", "a/b/x.dll")]
    [DataRow("./x.dll", "x.dll")]
    [DataRow("a/", "a")]
    [DataRow("CONSOLE.txt", "CONSOLE.txt")]
    [DataRow("COM10", "COM10")]
    [DataRow("COM0", "COM0")]
    [DataRow("..x.dll", "..x.dll")]
    [DataRow("x..dll", "x..dll")]
    [DataRow(".hidden", ".hidden")]
    public void A_safe_name_lands_inside_the_folder(string name, string expected)
    {
        string joined = SafePath.Join(_root, name);

        Assert.AreEqual(Path.Join([_root, .. expected.Split('/')]), joined);
    }

    [TestMethod]
    public void The_folder_keeps_the_callers_spelling()
    {
        string root = Path.Join("Games", "..", "Steam Library", "Game.");

        Assert.AreEqual(Path.Join(root, "bin", "x.dll"), SafePath.Join(root, "bin/x.dll"));
    }

    [TestMethod]
    [DataRow("nvngx_dlss.dll")]
    [DataRow("v1.2.3-x.zip")]
    [DataRow("COM10.log")]
    public void A_plain_file_name_is_accepted(string name) => SafePath.ValidateFileName(name);

    [TestMethod]
    [DataRow("a/x.dll")]
    [DataRow(@"a\x.dll")]
    [DataRow(".")]
    [DataRow("..")]
    [DataRow("")]
    [DataRow("aux.zip")]
    [DataRow("x.zip:stream")]
    [DataRow("x.zip.")]
    [DataRow("C:x.zip")]
    public void A_file_name_that_is_a_path_or_a_device_is_refused(string name)
    {
        UnsafePathException failure = Assert.ThrowsExactly<UnsafePathException>(() => SafePath.ValidateFileName(name));

        Assert.AreEqual(name, failure.Name);
    }
}
