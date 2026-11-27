using System.Net;
using Autopilot.Core.Components;
using Autopilot.Core.Net;
using Autopilot.Core.Tests.Net;

namespace Autopilot.Core.Tests.Components;

[TestClass]
public sealed class ReShadeTests(TestContext testContext)
{
    private const string Home = "https://reshade.me/";
    private const string Tags = "https://api.github.com/repos/crosire/reshade/tags?per_page=100";
    private const string Key = "0123456789abcdef0123456789abcdef";

    [TestMethod]
    public async Task The_add_on_installer_is_the_link_on_reshade_me()
    {
        using ComponentsBed bed = new(testContext);
        bed.Fixture(Home, "reshade-home.html");

        ReShadeInstaller installer = await bed.ReShade.ResolveAsync(bed.Cancellation);

        Assert.AreEqual("6.8.0", installer.Version.Label, "the add-on build, not the plain installer linked above it");
        Assert.AreEqual(new Uri("https://reshade.me/downloads/ReShade_Setup_6.8.0_Addon.exe"), installer.Url);
        Assert.AreEqual("ReShade_Setup_6.8.0_Addon.exe", installer.FileName);
        Assert.AreEqual(ReShadeOrigin.HomePage, installer.Origin);
        Assert.AreEqual(new NoVerification("reshade.me: no publisher sums or signature verified"), installer.Verification);
        Assert.AreEqual(0, bed.Server.RequestsTo(Tags));
    }

    [TestMethod]
    public async Task A_new_version_on_the_page_is_the_version_installed()
    {
        using ComponentsBed bed = new(testContext);
        bed.Server.Answer(Home, HttpStatusCode.OK, ReleaseFixtures.Fixture("reshade-home.html").Replace("6.8.0", "6.9.1", StringComparison.Ordinal));

        ReShadeInstaller installer = await bed.ReShade.ResolveAsync(bed.Cancellation);

        Assert.AreEqual(new Uri("https://reshade.me/downloads/ReShade_Setup_6.9.1_Addon.exe"), installer.Url);
    }

    [TestMethod]
    public async Task A_server_error_whose_page_still_holds_the_link_is_the_answer()
    {
        using ComponentsBed bed = new(testContext, TestBed.ImmediateRetries);
        bed.Fixture(Home, "reshade-home.html", HttpStatusCode.InternalServerError);

        ReShadeInstaller installer = await bed.ReShade.ResolveAsync(bed.Cancellation);

        Assert.AreEqual("6.8.0", installer.Version.Label);
        Assert.AreEqual(1, bed.Server.RequestsTo(Home), "no retry once the link is in hand");
    }

    [TestMethod]
    public async Task A_bare_server_error_is_retried_and_then_the_newest_github_tag_answers()
    {
        using ComponentsBed bed = new(testContext, TestBed.ImmediateRetries);
        bed.Fixture(Home, "reshade-500.html", HttpStatusCode.InternalServerError).Fixture(Tags, "reshade-tags.json");

        ReShadeInstaller installer = await bed.ReShade.ResolveAsync(bed.Cancellation);

        Assert.AreEqual(3, bed.Server.RequestsTo(Home), "the network client's three attempts");
        Assert.AreEqual(new Uri("https://reshade.me/downloads/ReShade_Setup_6.8.0_Addon.exe"), installer.Url);
        Assert.AreEqual(ReShadeOrigin.GitHubTag, installer.Origin);
    }

    [TestMethod]
    public async Task The_newest_tag_is_the_highest_version_not_the_first_listed()
    {
        using ComponentsBed bed = new(testContext);
        bed.Unreachable(Home);
        bed.Server.Answer(Tags, HttpStatusCode.OK, /*lang=json,strict*/ """[{"name":"v6.9.0"},{"name":"nightly"},{"name":"v7.0-beta"},{"name":"v6.10.0"},{"name":"6.11.0"},{"tag":"v9.0"}]""");

        ReShadeInstaller installer = await bed.ReShade.ResolveAsync(bed.Cancellation);

        Assert.AreEqual("6.10.0", installer.Version.Label);
    }

    [TestMethod]
    public async Task With_reshade_me_and_github_out_of_reach_the_newest_installer_in_the_cache_is_used()
    {
        using ComponentsBed bed = new(testContext);
        bed.Unreachable(Home).Unreachable(Tags);
        Keep(bed, Key + "-ReShade_Setup_6.7.1_Addon.exe");
        Keep(bed, Key[..31] + "1-ReShade_Setup_6.10.0_Addon.exe");
        Keep(bed, Key[..31] + "2-ReShade_Setup_6.11.0_Addon.exe.part");
        Keep(bed, Key[..31] + "3-ReShade_Setup_6.12.0_Addon.exe.lock");
        Keep(bed, "ReShade_Setup_6.13.0_Addon.exe");
        Keep(bed, Key[..31] + "4-ReShade_Setup_6.14.0.exe");

        ReShadeInstaller installer = await bed.ReShade.ResolveAsync(bed.Cancellation);

        Assert.AreEqual("6.10.0", installer.Version.Label, "an unfinished download, a lock and files of other shapes are not kept installers");
        Assert.AreEqual(ReShadeOrigin.Cache, installer.Origin);
        Assert.AreEqual(new Uri("https://reshade.me/downloads/ReShade_Setup_6.10.0_Addon.exe"), installer.Url);
    }

    [TestMethod]
    public async Task When_every_source_fails_the_error_lists_each_reason()
    {
        using ComponentsBed bed = new(testContext);
        bed.Server.Answer(Home, HttpStatusCode.OK, "<html><body>Maintenance</body></html>").Answer(Tags, HttpStatusCode.NotFound, "{}");

        ComponentException failure = await Assert.ThrowsAsync<ComponentException>(() => bed.ReShade.ResolveAsync(bed.Cancellation));

        string[] reasons =
        [
            "reshade.me: HTTP 200, with no add-on installer link on the page",
            "GitHub: no version tag on crosire/reshade (HTTP 404)",
            "the download cache: no ReShade add-on installer kept",
        ];
        Assert.AreEqual(ComponentFailure.InstallerNotFound, failure.Reason);
        CollectionAssert.AreEqual(reasons, failure.Reasons.ToArray());
        Assert.AreEqual("Could not find the ReShade add-on installer: " + string.Join("; ", reasons), failure.Message);
    }

    [TestMethod]
    [DataRow(/*lang=json,strict*/ "{\"name\":\"v6.8.0\"}")]
    [DataRow("not json")]
    [DataRow("[]")]
    public async Task A_tag_answer_that_names_no_version_is_a_reason_not_a_crash(string body)
    {
        using ComponentsBed bed = new(testContext);
        bed.Unreachable(Home).Server.Answer(Tags, HttpStatusCode.OK, body);

        ComponentException failure = await Assert.ThrowsAsync<ComponentException>(() => bed.ReShade.ResolveAsync(bed.Cancellation));

        Assert.HasCount(3, failure.Reasons);
        StringAssert.Contains(failure.Reasons[0], "reshade.me", StringComparison.Ordinal);
        Assert.AreEqual("GitHub: no version tag on crosire/reshade (HTTP 200)", failure.Reasons[1]);
    }

    [TestMethod]
    public async Task A_network_failure_is_named_in_the_reasons()
    {
        using ComponentsBed bed = new(testContext);
        bed.Unreachable(Home).Unreachable(Tags);

        ComponentException failure = await Assert.ThrowsAsync<ComponentException>(() => bed.ReShade.ResolveAsync(bed.Cancellation));

        StringAssert.StartsWith(failure.Reasons[0], "reshade.me", StringComparison.Ordinal);
        StringAssert.StartsWith(failure.Reasons[1], "api.github.com", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task A_redirect_off_the_list_is_refused_and_not_covered_by_another_source()
    {
        using ComponentsBed bed = new(testContext);
        bed.Server.Redirect(Home, "https://evil.test/");

        NetException failure = await Assert.ThrowsAsync<NetException>(() => bed.ReShade.ResolveAsync(bed.Cancellation));

        Assert.AreEqual(NetErrorKind.RedirectRefused, failure.Kind);
        Assert.AreEqual(0, bed.Server.RequestsTo(Tags));
    }

    [TestMethod]
    public async Task ReShade_is_resolved_once_per_run()
    {
        using ComponentsBed bed = new(testContext);
        bed.Fixture(Home, "reshade-home.html");

        ReShadeInstaller first = await bed.ReShade.ResolveAsync(bed.Cancellation);
        ReShadeInstaller again = await bed.ReShade.ResolveAsync(bed.Cancellation);

        Assert.AreSame(first, again);
        Assert.AreEqual(1, bed.Server.RequestsTo(Home));
    }

    [TestMethod]
    public async Task A_failed_resolution_is_not_remembered()
    {
        using ComponentsBed bed = new(testContext);
        bed.Unreachable(Home).Unreachable(Tags).Fixture(Home, "reshade-home.html");

        _ = await Assert.ThrowsAsync<ComponentException>(() => bed.ReShade.ResolveAsync(bed.Cancellation));
        ReShadeInstaller installer = await bed.ReShade.ResolveAsync(bed.Cancellation);

        Assert.AreEqual(ReShadeOrigin.HomePage, installer.Origin);
    }

    [TestMethod]
    public async Task An_installer_found_only_in_the_cache_is_not_remembered()
    {
        using ComponentsBed bed = new(testContext);
        bed.Unreachable(Home).Unreachable(Tags).Fixture(Home, "reshade-home.html");
        Keep(bed, Key + "-ReShade_Setup_6.7.1_Addon.exe");

        ReShadeInstaller offline = await bed.ReShade.ResolveAsync(bed.Cancellation);
        ReShadeInstaller online = await bed.ReShade.ResolveAsync(bed.Cancellation);

        Assert.AreEqual(ReShadeOrigin.Cache, offline.Origin);
        Assert.AreEqual(ReShadeOrigin.HomePage, online.Origin);
    }

    private static void Keep(ComponentsBed bed, string entryName)
    {
        string folder = Path.Combine(bed.Net.CacheRoot, "files");
        _ = Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, entryName), "MZ"u8.ToArray());
    }
}
