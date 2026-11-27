using System.Net;
using Autopilot.Core.Components;
using Autopilot.Core.Components.GitHub;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Components.GitHub;

[TestClass]
public sealed class ReleaseApiTests(TestContext testContext)
{
    private const string Dxvk32Digest = "17449ed21602377971289eb2984750699825ac1bac3bbfa2426b2bba0f355692";

    private static readonly string[] _dxvkNewest = ["v3.2", "v3.1.1", "v3.1", "v3.0.2", "v3.0.1"];
    private static readonly string[] _dxvk311Assets = ["dxvk-3.1.1.tar.gz", "dxvk-native-3.1.1-steamrt-sniper.tar.gz"];

    [TestMethod]
    public async Task The_release_list_comes_from_the_api_newest_first()
    {
        using ReleasesBed bed = new(testContext);
        bed.Server.Answer(ReleasesBed.ApiList(5), HttpStatusCode.OK, ReleaseFixtures.Fixture("dxvk-releases.json"));

        ReleaseListAnswer answer = await bed.List(5);

        CollectionAssert.AreEqual(_dxvkNewest, ReleaseFixtures.Tags(answer.Releases));
        CollectionAssert.AreEqual(_dxvk311Assets, ReleaseFixtures.AssetNames(answer.Releases[1]));
        Assert.AreEqual(new ReleaseSource(ReleaseOrigin.Api, TimeSpan.Zero), answer.Source);
        Assert.IsNull(answer.Source.Notice);
    }

    [TestMethod]
    public async Task A_listed_release_carries_its_flags_date_and_assets()
    {
        using ReleasesBed bed = new(testContext);
        bed.Server.Answer(ReleasesBed.ApiList(5), HttpStatusCode.OK, ReleaseFixtures.Fixture("dxvk-releases.json"));

        GitHubRelease newest = (await bed.List(5)).Releases[0];

        Assert.IsFalse(newest.Prerelease);
        Assert.IsFalse(newest.Draft);
        Assert.AreEqual(new DateTimeOffset(2026, 11, 26, 16, 1, 7, TimeSpan.Zero), newest.PublishedAt);
        Assert.AreEqual(
            new GitHubAsset("dxvk-3.2.tar.gz", new("https://github.com/doitsujin/dxvk/releases/download/v3.2/dxvk-3.2.tar.gz"), new(Dxvk32Digest)),
            Assert.ContainsSingle(newest.Assets));
    }

    [TestMethod]
    public async Task The_latest_release_carries_the_sha256_digest_the_api_gives()
    {
        using ReleasesBed bed = new(testContext);
        bed.Server.Answer(ReleasesBed.ApiLatest, HttpStatusCode.OK, ReleaseFixtures.Fixture("dxvk-latest.json"));

        ReleaseAnswer? answer = await bed.Latest();

        Assert.IsNotNull(answer);
        Assert.AreEqual("v3.2", answer.Release.Tag);
        Assert.AreEqual(new Sha256Verification(Dxvk32Digest), Assert.ContainsSingle(answer.Release.Assets).Digest);
        Assert.AreEqual(ReleaseOrigin.Api, answer.Source.Origin);
    }

    [TestMethod]
    public async Task A_release_by_tag_comes_from_the_api()
    {
        using ReleasesBed bed = new(testContext);
        bed.Server.Answer(ReleasesBed.ApiTag, HttpStatusCode.OK, ReleaseFixtures.Fixture("dxvk-latest.json"));

        ReleaseAnswer? answer = await bed.ByTag("v3.2");

        Assert.AreEqual("v3.2", answer?.Release.Tag);
        Assert.AreEqual(1, bed.RequestsTo(ReleasesBed.ApiTag));
    }

    [TestMethod]
    public async Task A_release_the_api_does_not_know_is_no_release()
    {
        using ReleasesBed bed = new(testContext);
        bed.Server.Answer(ReleasesBed.ApiTag, HttpStatusCode.NotFound, /*lang=json,strict*/ """{"message":"Not Found","status":"404"}""");
        bed.Server.Answer(ReleasesBed.ApiLatest, HttpStatusCode.NotFound, /*lang=json,strict*/ """{"message":"Not Found","status":"404"}""");

        Assert.IsNull(await bed.ByTag("v3.2"));
        Assert.IsNull(await bed.Latest());
        Assert.AreEqual(2, bed.Server.Requests.Count, "a not found is the answer: the pages are not asked");
    }

    [TestMethod]
    public async Task A_repository_the_api_does_not_know_fails_the_list()
    {
        using ReleasesBed bed = new(testContext);
        bed.Server.Answer(ReleasesBed.ApiList(5), HttpStatusCode.NotFound, /*lang=json,strict*/ """{"message":"Not Found","status":"404"}""");

        NetException failure = await Assert.ThrowsAsync<NetException>(() => bed.List(5));

        Assert.AreEqual(new HttpStatusDetail(HttpStatusCode.NotFound), failure.Detail);
        Assert.HasCount(1, bed.Server.Requests);
    }

    [TestMethod]
    public async Task Another_status_from_the_api_is_a_failure_with_that_status()
    {
        using ReleasesBed bed = new(testContext);
        bed.Server.Answer(ReleasesBed.ApiLatest, HttpStatusCode.Unauthorized, /*lang=json,strict*/ """{"message":"Bad credentials"}""");

        NetException failure = await Assert.ThrowsAsync<NetException>(bed.Latest);

        Assert.AreEqual(new HttpStatusDetail(HttpStatusCode.Unauthorized), failure.Detail);
    }

    [TestMethod]
    [DataRow("owner", DisplayName = "no name")]
    [DataRow("owner/name/more", DisplayName = "a path")]
    [DataRow("owner/..", DisplayName = "a parent folder")]
    [DataRow("-owner/name", DisplayName = "an owner starting with a dash")]
    [DataRow("owner/na me", DisplayName = "a space")]
    public async Task A_repository_that_is_not_owner_slash_name_is_refused_without_a_request(string repository)
    {
        using ReleasesBed bed = new(testContext);

        _ = await Assert.ThrowsAsync<ArgumentException>(() => bed.Releases.GetLatestAsync(repository, testContext.CancellationToken));

        Assert.IsEmpty(bed.Server.Requests);
    }

    [TestMethod]
    public async Task A_list_count_outside_what_the_api_lists_is_refused()
    {
        using ReleasesBed bed = new(testContext);

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => bed.List(0));
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => bed.List(GitHubReleases.MaxListCount + 1));
    }
}
