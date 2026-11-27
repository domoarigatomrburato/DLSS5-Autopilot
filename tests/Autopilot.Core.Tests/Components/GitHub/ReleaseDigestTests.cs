using System.Net;
using Autopilot.Core.Components;
using Autopilot.Core.Components.GitHub;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Components.GitHub;

[TestClass]
public sealed class ReleaseDigestTests(TestContext testContext)
{
    private const string Dxvk32Digest = "17449ed21602377971289eb2984750699825ac1bac3bbfa2426b2bba0f355692";

    [TestMethod]
    public async Task Pre_release_flags_are_read()
    {
        using ReleasesBed bed = new(testContext);
        ReleaseListAnswer answer = await BridgeList(bed);

        Assert.IsTrue(answer.Releases.All(release => release.Prerelease == true));
    }

    [TestMethod]
    public async Task Every_asset_carries_the_digest_the_api_gives()
    {
        using ReleasesBed bed = new(testContext);
        GitHubRelease newest = (await BridgeList(bed)).Releases[0];

        Assert.AreEqual(
            "c4c8b5bc4b26b2b3f3bf2767cdb708546d62f7d0bbb63d24e940c736da9efe26",
            newest.Assets.Single(asset => asset.Name == "dlss5-bridge.addon64").Digest?.Digest);
        Assert.IsTrue(newest.Assets.All(asset => asset.Digest is not null));
    }

    [TestMethod]
    [DataRow("md5:0123456789abcdef0123456789abcdef", DisplayName = "another algorithm")]
    [DataRow("sha256:1234", DisplayName = "too short")]
    [DataRow("sha256:zz449ed21602377971289eb2984750699825ac1bac3bbfa2426b2bba0f355692", DisplayName = "not hex")]
    public async Task A_digest_that_is_not_a_sha256_is_no_digest(string digest)
    {
        using ReleasesBed bed = new(testContext);
        bed.Server.Answer(ReleasesBed.ApiLatest, HttpStatusCode.OK, ReleaseJson("v1", $$"""[{"name":"a.zip","browser_download_url":"https://github.com/o/r/a.zip","digest":"{{digest}}"}]"""));

        ReleaseAnswer? answer = await bed.Latest();

        Assert.IsNull(Assert.ContainsSingle(answer!.Release.Assets).Digest);
    }

    [TestMethod]
    public async Task A_digest_in_capitals_is_kept_in_lowercase()
    {
        using ReleasesBed bed = new(testContext);
        string upper = "SHA256:" + Dxvk32Digest.ToUpperInvariant();
        bed.Server.Answer(ReleasesBed.ApiLatest, HttpStatusCode.OK, ReleaseJson("v1", $$"""[{"name":"a.zip","browser_download_url":"https://github.com/o/r/a.zip","digest":"{{upper}}"}]"""));

        ReleaseAnswer? answer = await bed.Latest();

        Assert.AreEqual(Dxvk32Digest, Assert.ContainsSingle(answer!.Release.Assets).Digest?.Digest);
    }

    [TestMethod]
    public async Task An_asset_read_from_the_pages_carries_the_digest_its_row_shows()
    {
        using ReleasesBed bed = new(testContext);
        bed.Unreachable(ReleasesBed.ApiTag).Fragments("v3.2");

        ReleaseAnswer? answer = await bed.ByTag("v3.2");

        Assert.AreEqual(ReleaseOrigin.Pages, answer?.Source.Origin);
        Assert.AreEqual(new Sha256Verification(Dxvk32Digest), Assert.ContainsSingle(answer!.Release.Assets).Digest);
    }

    [TestMethod]
    [DataRow("md5:0123456789abcdef0123456789abcdef", DisplayName = "another algorithm")]
    [DataRow("sha256:1234", DisplayName = "too short")]
    [DataRow("sha256:zz449ed21602377971289eb2984750699825ac1bac3bbfa2426b2bba0f355692", DisplayName = "not hex")]
    public async Task A_digest_on_the_pages_that_is_not_a_sha256_is_no_digest(string digest)
    {
        using ReleasesBed bed = new(testContext);
        bed.Unreachable(ReleasesBed.ApiTag).Server.Answer(
            ReleasesBed.Expanded("v3.2"),
            HttpStatusCode.OK,
            ReleaseFixtures.ExpandedFor("v3.2").Replace("sha256:" + Dxvk32Digest, digest, StringComparison.Ordinal));

        ReleaseAnswer? answer = await bed.ByTag("v3.2");

        Assert.IsNull(Assert.ContainsSingle(answer!.Release.Assets).Digest);
    }

    [TestMethod]
    public async Task A_digest_on_the_pages_in_capitals_is_kept_in_lowercase()
    {
        using ReleasesBed bed = new(testContext);
        bed.Unreachable(ReleasesBed.ApiTag).Server.Answer(
            ReleasesBed.Expanded("v3.2"),
            HttpStatusCode.OK,
            ReleaseFixtures.ExpandedFor("v3.2").Replace(Dxvk32Digest, Dxvk32Digest.ToUpperInvariant(), StringComparison.Ordinal));

        ReleaseAnswer? answer = await bed.ByTag("v3.2");

        Assert.AreEqual(Dxvk32Digest, Assert.ContainsSingle(answer!.Release.Assets).Digest?.Digest);
    }

    private Task<ReleaseListAnswer> BridgeList(ReleasesBed bed)
    {
        bed.Server.Answer(ComponentsBed.ApiList("NIGos/dlss5-bridge", 2), HttpStatusCode.OK, ReleaseFixtures.Fixture("dlss5-bridge-releases.json"));
        return bed.Releases.GetListAsync("NIGos/dlss5-bridge", 2, testContext.CancellationToken);
    }

    private static string ReleaseJson(string tag, string assets) => $$"""{"tag_name":"{{tag}}","prerelease":false,"draft":false,"assets":{{assets}}}""";
}
