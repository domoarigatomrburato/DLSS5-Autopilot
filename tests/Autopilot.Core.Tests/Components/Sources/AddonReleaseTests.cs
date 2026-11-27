using System.Net;
using System.Text.Json.Nodes;
using Autopilot.Core.Components;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Components.Sources;

/// <summary>The add-ons taken from a repository's newest release: dlss5-bridge, neural-upstream, standalone-dlssnr, the Remix runtime.</summary>
[TestClass]
public sealed class AddonReleaseTests(TestContext testContext)
{
    private const string Bridge = "NIGos/dlss5-bridge";
    private const string Upstream = "matiasLombo/neural-upstream";
    private const string Standalone = "kibblerz/DLSS5-Reshade-AIO";
    private const string Remix = "lunks/dxvk-remix-plus-dlssnr";

    [TestMethod]
    public async Task The_bridge_is_the_newest_releases_add_on_with_its_digest()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Fixture(ComponentsBed.Api(Bridge, "/latest"), "dlss5-bridge-latest.json");

        ComponentRelease release = await bed.Resolve(ComponentIds.Bridge);

        Assert.AreEqual("v1.4.12", release.Version.Label);
        ComponentDownload addon = release.Downloads.Single();
        Assert.AreEqual("dlss5-bridge.addon64", addon.FileName);
        Assert.AreEqual(new Uri(ComponentsBed.Download(Bridge, "v1.4.12", "dlss5-bridge.addon64")), addon.Url);
        Assert.IsInstanceOfType<Sha256Verification>(addon.Verification);
    }

    [TestMethod]
    public async Task The_bridge_add_on_is_a_loose_file_it_must_be_and_the_api_needs_no_words()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Fixture(ComponentsBed.Api(Bridge, "/latest"), "dlss5-bridge-latest.json");

        ComponentRelease release = await bed.Resolve(ComponentIds.Bridge);

        Assert.IsFalse(release.Downloads[0].IsArchive);
        Assert.AreEqual("dlss5-bridge.addon64", release.Files.Single().Path);
        Assert.IsNull(release.Notice);
    }

    [TestMethod]
    public async Task A_component_is_resolved_once_per_run()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Fixture(ComponentsBed.Api(Bridge, "/latest"), "dlss5-bridge-latest.json");

        ComponentRelease first = await bed.Resolve(ComponentIds.Bridge);
        ComponentRelease second = await bed.Resolve(ComponentIds.Bridge);

        Assert.AreSame(first, second);
        Assert.AreEqual(1, bed.Server.RequestsTo(ComponentsBed.Api(Bridge, "/latest")));
    }

    [TestMethod]
    public async Task A_bridge_release_without_an_add_on_is_a_changed_layout()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Json(ComponentsBed.Api(Bridge, "/latest"), ReleaseJsonText.Release(Bridge, "v2.0.0", 0, "dlss5-bridge-v2.0.0.zip"));

        ComponentException failure = await Assert.ThrowsAsync<ComponentException>(() => bed.Resolve(ComponentIds.Bridge));

        Assert.AreEqual(ComponentFailure.LayoutChanged, failure.Reason);
        Assert.AreEqual(
            "NIGos/dlss5-bridge v2.0.0 does not carry .addon64: the release changed its layout, and a newer layout is not taken on trust. Nothing was placed.",
            failure.Message);
    }

    [TestMethod]
    public async Task A_repository_without_a_release_says_so()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Fixture(ComponentsBed.Api(Bridge, "/latest"), "rate-limited-403.json", HttpStatusCode.NotFound);

        ComponentException failure = await Assert.ThrowsAsync<ComponentException>(() => bed.Resolve(ComponentIds.Bridge));

        Assert.AreEqual(ComponentFailure.NoRelease, failure.Reason);
        StringAssert.StartsWith(failure.Message, "NIGos/dlss5-bridge has no release on GitHub.", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task Without_release_data_the_bridge_fails_with_the_network()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).NoReleaseData(Bridge);

        _ = await Assert.ThrowsAsync<NetException>(() => bed.Resolve(ComponentIds.Bridge));
    }

    [TestMethod]
    public async Task Upstream_is_the_asset_named_nvngx_dll_addon64()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Fixture(ComponentsBed.Api(Upstream, "/latest"), "neural-upstream-latest.json");

        ComponentRelease release = await bed.Resolve(ComponentIds.Upstream);

        Assert.AreEqual("v0.3.0", release.Version.Label);
        Assert.AreEqual("nvngx.dll.addon64", release.Downloads.Single().FileName);
    }

    [TestMethod]
    public async Task An_upstream_release_without_its_add_on_is_a_changed_layout()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Json(ComponentsBed.Api(Upstream, "/latest"), ReleaseJsonText.Release(Upstream, "v1.0.0", 0, "neural-upstream.addon64"));

        ComponentException failure = await Assert.ThrowsAsync<ComponentException>(() => bed.Resolve(ComponentIds.Upstream));

        Assert.AreEqual(ComponentFailure.LayoutChanged, failure.Reason);
        StringAssert.Contains(failure.Message, "does not carry nvngx.dll.addon64", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task With_no_release_data_upstream_comes_from_the_latest_download_redirect()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).NoReleaseData(Upstream);

        ComponentRelease release = await bed.Resolve(ComponentIds.Upstream);

        Assert.AreSame(ComponentVersion.Latest, release.Version);
        Assert.IsFalse(release.Version.IsComparable);
        Assert.AreEqual(VersionOrder.NotComparable, release.Version.CompareTo(new ComponentVersion("1.0.0")));
        ComponentDownload addon = release.Downloads.Single();
        Assert.AreEqual(new Uri("https://github.com/matiasLombo/neural-upstream/releases/latest/download/nvngx.dll.addon64"), addon.Url);
        Assert.AreEqual(new NoVerification("matiasLombo/neural-upstream: taken with no release data, so no digest is known"), addon.Verification);
        Assert.IsNull(release.Source);
        Assert.Contains("the exact version is whatever that redirect points at", release.Notice!);
    }

    [TestMethod]
    public async Task The_standalone_release_of_today_is_its_64_bit_zip()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Fixture(ComponentsBed.Api(Standalone, "/latest"), "standalone-latest.json");

        ComponentRelease release = await bed.Resolve(ComponentIds.Standalone);

        Assert.AreEqual("v2.2.4", release.Version.Label);
        ComponentDownload archive = release.Downloads.Single();
        Assert.AreEqual("DLSS5-ReShade-AIO-v2.2.4-64-bit.zip", archive.FileName);
        Assert.IsTrue(archive.IsArchive);
        Assert.AreSequenceEqual(
            ["standalone-dlssnr.addon64", "nvngx.dll", "DLSS5_AIO_Feed.fx", "StandaloneBoundary.fx"],
            [.. release.Files.Select(file => file.Path)]);
        Assert.IsTrue(release.Files[^1].Optional, "older releases have no boundary shader");
    }

    [TestMethod]
    public async Task A_standalone_release_of_loose_files_takes_them_and_its_boundary_shader()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Json(
            ComponentsBed.Api(Standalone, "/latest"),
            ReleaseJsonText.Release(Standalone, "v2.0.5", 0, "DLSS5_AIO_Feed.fx", "nvngx.dll", "StandaloneBoundary.fx", "standalone-dlssnr.addon64", "DLSS5-ReShade-AIO-v2.0.5-64-bit.zip"));

        ComponentRelease release = await bed.Resolve(ComponentIds.Standalone);

        Assert.AreSequenceEqual(
            ["DLSS5_AIO_Feed.fx", "nvngx.dll", "standalone-dlssnr.addon64", "StandaloneBoundary.fx"],
            [.. release.Downloads.Select(download => download.FileName)]);
        Assert.AreEqual(
            new NoVerification("kibblerz/DLSS5-Reshade-AIO: the release data gave no digest"),
            release.Downloads[0].Verification);
    }

    [TestMethod]
    public async Task A_standalone_release_of_neither_shape_is_a_changed_layout()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Json(
            ComponentsBed.Api(Standalone, "/latest"),
            ReleaseJsonText.Release(Standalone, "v3.0.0", 0, "nvngx.dll", "DLSS5-ReShade-AIO-v3.0.0-32-bit.zip"));

        ComponentException failure = await Assert.ThrowsAsync<ComponentException>(() => bed.Resolve(ComponentIds.Standalone));

        Assert.AreEqual(ComponentFailure.LayoutChanged, failure.Reason);
        StringAssert.Contains(failure.Message, "does not carry standalone-dlssnr.addon64, nvngx.dll, DLSS5_AIO_Feed.fx or a -64-bit.zip", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task With_no_release_data_the_standalone_archive_is_named_after_the_redirects_tag_and_asked_first()
    {
        string archive = ComponentsBed.Download(Standalone, "v2.2.4", "DLSS5-ReShade-AIO-v2.2.4-64-bit.zip");
        using ComponentsBed bed = NoDataButRedirect(archive, HttpStatusCode.Found);

        ComponentRelease release = await bed.Resolve(ComponentIds.Standalone);

        Assert.AreEqual("v2.2.4", release.Version.Label);
        Assert.AreEqual(new Uri(archive), release.Downloads.Single().Url);
        Assert.AreSequenceEqual([HttpMethod.Head], [.. bed.Server.Requests.Where(request => request.Url == new Uri(archive)).Select(request => request.Method)]);
        Assert.AreEqual(1, bed.Server.RequestsTo(ComponentsBed.LatestPage(Standalone)), "the pages' fallback and the guessed name share one redirect");
        Assert.AreEqual("GitHub's API could not be reached; this release was read from github.com's release pages instead.", release.Notice);
    }

    [TestMethod]
    public async Task With_no_archive_under_the_guessed_name_the_loose_files_come_by_redirect()
    {
        using ComponentsBed bed = NoDataButRedirect(ComponentsBed.Download(Standalone, "v2.2.4", "DLSS5-ReShade-AIO-v2.2.4-64-bit.zip"), HttpStatusCode.NotFound);

        ComponentRelease release = await bed.Resolve(ComponentIds.Standalone);

        Assert.AreSame(ComponentVersion.Latest, release.Version);
        Assert.AreSequenceEqual(
            ["standalone-dlssnr.addon64", "nvngx.dll", "DLSS5_AIO_Feed.fx"],
            [.. release.Downloads.Select(download => download.Url.Segments[^1])]);
    }

    [TestMethod]
    public async Task With_github_com_silent_too_the_loose_files_come_by_redirect()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).NoReleaseData(Standalone);

        ComponentRelease release = await bed.Resolve(ComponentIds.Standalone);

        Assert.AreSame(ComponentVersion.Latest, release.Version);
        Assert.HasCount(3, release.Downloads);
    }

    [TestMethod]
    public async Task The_remix_runtime_is_checked_against_its_sums_file()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Fixture(ComponentsBed.Api(Remix, "/latest"), "remix-runtime-latest.json");

        ComponentRelease release = await bed.Resolve(ComponentIds.RemixRuntime);

        Assert.AreEqual("dlssnr-v1", release.Version.Label);
        Uri sums = new(ComponentsBed.Download(Remix, "dlssnr-v1", "SHA256SUMS.txt"));
        Assert.AreSequenceEqual(
            [new SumsFileVerification(sums, "d3d9.dll"), new SumsFileVerification(sums, "remix_nvngx.dll")],
            release.Downloads.Select(download => download.Verification).ToArray());
    }

    [TestMethod]
    public async Task A_remix_runtime_without_sums_falls_back_to_the_digest_then_the_magic_bytes()
    {
        JsonObject latest = ReleaseJsonText.Release(Remix, "dlssnr-v2", 0, "d3d9.dll", "remix_nvngx.dll");
        latest["assets"]![0]!["digest"] = "sha256:" + new string('a', 64);
        using ComponentsBed bed = new ComponentsBed(testContext).Json(ComponentsBed.Api(Remix, "/latest"), latest);

        ComponentRelease release = await bed.Resolve(ComponentIds.RemixRuntime);

        Assert.AreEqual(new Sha256Verification(new string('a', 64)), release.Downloads[0].Verification);
        Assert.AreEqual(new NoVerification("lunks/dxvk-remix-plus-dlssnr: the release data gave no digest"), release.Downloads[1].Verification);
    }

    [TestMethod]
    public async Task A_remix_release_without_both_files_is_a_changed_layout()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).Json(
            ComponentsBed.Api(Remix, "/latest"),
            ReleaseJsonText.Release(Remix, "capifix31", 0, "rtx-remix-for-x64-games-83-b687a4f-release.zip", "d3d9.dll"));

        ComponentException failure = await Assert.ThrowsAsync<ComponentException>(() => bed.Resolve(ComponentIds.RemixRuntime));

        Assert.AreEqual(ComponentFailure.LayoutChanged, failure.Reason);
        StringAssert.Contains(failure.Message, "capifix31 does not carry remix_nvngx.dll:", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task With_no_release_data_both_remix_files_come_by_redirect()
    {
        using ComponentsBed bed = new ComponentsBed(testContext).NoReleaseData(Remix);

        ComponentRelease release = await bed.Resolve(ComponentIds.RemixRuntime);

        Assert.AreSame(ComponentVersion.Latest, release.Version);
        Assert.AreSequenceEqual(["d3d9.dll", "remix_nvngx.dll"], [.. release.Files.Select(file => file.Path)]);
    }

    // The API and the asset pages fail; the latest-release redirect still names v2.2.4, and the guessed archive answers its HEAD with status.
    private ComponentsBed NoDataButRedirect(string archive, HttpStatusCode archiveStatus)
    {
        ComponentsBed bed = new ComponentsBed(testContext)
            .Unreachable(ComponentsBed.Api(Standalone, "/latest"))
            .Unreachable(ComponentsBed.Expanded(Standalone, "v2.2.4"));
        _ = bed.Server.Redirect(ComponentsBed.LatestPage(Standalone), $"https://github.com/{Standalone}/releases/tag/v2.2.4");
        _ = archiveStatus == HttpStatusCode.Found
            ? bed.Server.Redirect(archive, "https://release-assets.githubusercontent.com/github-production-release-asset/1/2")
            : bed.Server.Answer(archive, archiveStatus);
        return bed;
    }
}
