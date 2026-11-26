using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Net;

/// <summary>A file that does not start the way its name says never reaches the cache.</summary>
[TestClass]
public sealed class WrongContentTests(TestContext testContext)
{
    private static readonly NoVerification _none = new("test");

    [TestMethod]
    public async Task An_html_page_saved_as_a_zip_is_refused_as_a_page_standing_in_for_the_file()
    {
        using CacheBed bed = new CacheBed(testContext).Serve(CacheFixtures.Page);

        NetException failure = await bed.DownloadFails(_none);

        Assert.AreEqual(NetErrorKind.WrongContent, failure.Kind);
        Assert.AreEqual("<!DOCTYPE html><", FileStartOf(failure));
        Assert.Contains("proxy", NetExplanations.Explain(failure));
        Assert.Contains(CacheBed.Name, NetExplanations.Explain(failure));
        Assert.IsEmpty(bed.Files.Cached);
        Assert.AreEqual(1, bed.Requests, "the same page would come back");
    }

    [TestMethod]
    public async Task A_zip_cut_before_its_central_directory_is_refused()
    {
        using CacheBed bed = new CacheBed(testContext).Serve(CacheFixtures.Zip[..40]);

        NetException failure = await bed.DownloadFails(_none);

        Assert.AreEqual(NetErrorKind.WrongContent, failure.Kind);
        Assert.StartsWith(@"PK\x03\x04", FileStartOf(failure));
        Assert.IsEmpty(bed.Files.Cached);
    }

    [TestMethod]
    [DataRow("tool.7z", new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C, 0 })]
    [DataRow("ReShade.addon64", new byte[] { 0x4D, 0x5A, 0x90 })]
    [DataRow("ReShade.addon32", new byte[] { 0x4D, 0x5A, 0x90 })]
    [DataRow("SETUP.EXE", new byte[] { 0x4D, 0x5A, 0x90 })]
    [DataRow("dxvk.tar.gz", new byte[] { 0x1F, 0x8B, 8 })]
    [DataRow("notes.txt", new byte[] { 0x3C, 0x68 })]
    public async Task A_file_that_starts_the_way_its_name_says_is_kept(string name, byte[] body)
    {
        using CacheBed bed = new CacheBed(testContext).Serve(body);

        CachedFile file = await bed.Download(_none, name);

        Assert.AreEqual(body.Length, file.Size);
    }

    [TestMethod]
    [DataRow("tool.7z")]
    [DataRow("dxgi.dll")]
    [DataRow("dxvk.tgz")]
    [DataRow("tool.zip")]
    public async Task A_file_that_does_not_start_the_way_its_name_says_is_refused(string name)
    {
        using CacheBed bed = new CacheBed(testContext).Serve([0, 1, 0x5C]);

        NetException failure = await bed.DownloadFails(_none, name);

        Assert.AreEqual(@"\x00\x01\x5c", FileStartOf(failure));
        Assert.DoesNotContain("proxy", NetExplanations.Explain(failure));
    }

    private static string FileStartOf(NetException failure) =>
        Assert.IsInstanceOfType<WrongContentDetail>(failure.Detail).FileStart;

    [TestMethod]
    public async Task A_page_after_a_byte_order_mark_and_blank_lines_is_still_a_page()
    {
        using CacheBed bed = new CacheBed(testContext).Serve([0xEF, 0xBB, 0xBF, (byte)'\r', (byte)'\n', (byte)' ', .. CacheFixtures.Page]);

        NetException failure = await bed.DownloadFails(_none);

        Assert.Contains("proxy", NetExplanations.Explain(failure));
    }
}
