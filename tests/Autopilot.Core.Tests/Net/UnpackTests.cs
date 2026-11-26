using Autopilot.Core.Archives;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Net;

[TestClass]
public sealed class UnpackTests(TestContext testContext)
{
    [TestMethod]
    public async Task Unpacking_the_same_archive_twice_reuses_its_folder()
    {
        using CacheBed bed = new CacheBed(testContext).Serve(CacheFixtures.Zip);
        CachedFile file = await bed.Download();

        string folder = await bed.Unpack(file, new() { StripTopFolder = true });
        await File.WriteAllTextAsync(Path.Combine(folder, "kept.txt"), "", testContext.CancellationToken);
        string again = await bed.Unpack(file, new() { StripTopFolder = true });

        Assert.AreEqual(folder, again);
        Assert.IsTrue(CacheFiles.Holds(Path.Combine(folder, "readme.txt"), "hello"u8.ToArray()));
        Assert.IsTrue(File.Exists(Path.Combine(folder, "kept.txt")), "a finished folder is never rewritten");
        Assert.AreEqual(bed.Files.UnpackedFolder, Path.GetDirectoryName(folder));
    }

    [TestMethod]
    public async Task Unpacking_with_other_options_gets_its_own_folder()
    {
        using CacheBed bed = new CacheBed(testContext).Serve(CacheFixtures.Zip);
        CachedFile file = await bed.Download();

        string whole = await bed.Unpack(file);
        string filtered = await bed.Unpack(file, new() { StripTopFolder = true, Extensions = [".TXT"] }, new() { MaxEntries = 10 });

        Assert.AreNotEqual(whole, filtered);
        Assert.IsTrue(File.Exists(Path.Combine(whole, "tool", "readme.txt")));
        Assert.IsTrue(File.Exists(Path.Combine(filtered, "readme.txt")));
    }

    [TestMethod]
    public async Task Options_that_differ_only_in_case_or_order_share_a_folder()
    {
        using CacheBed bed = new CacheBed(testContext).Serve(CacheFixtures.Zip);
        CachedFile file = await bed.Download();

        string first = await bed.Unpack(file, new TreeOptions { Extensions = [".txt", ".DLL"], SourceFolder = "Tool" });
        string second = await bed.Unpack(file, new TreeOptions { Extensions = [".dll", ".TXT"], SourceFolder = "tool" });

        Assert.AreEqual(first, second);
    }
}
