using System.Net;
using Autopilot.Core.Components;

namespace Autopilot.Core.Tests.Components;

[TestClass]
public sealed class ReleaseDataTests(TestContext testContext)
{
    [TestMethod]
    [DataRow("{ not json", DisplayName = "not JSON")]
    [DataRow("", DisplayName = "empty")]
    [DataRow("null", DisplayName = "null")]
    [DataRow("[]", DisplayName = "a list where one release belongs")]
    [DataRow(/*lang=json,strict*/ """{"name":"v1"}""", DisplayName = "no tag")]
    [DataRow(/*lang=json,strict*/ """{"tag_name":"v1","assets":[{"name":"a.zip"}]}""", DisplayName = "an asset without an address")]
    [DataRow(/*lang=json,strict*/ """{"tag_name":"v1","assets":[{"browser_download_url":"https://github.com/o/r/a.zip"}]}""", DisplayName = "an asset without a name")]
    [DataRow(/*lang=json,strict*/ """{"tag_name":"v1","assets":[null]}""", DisplayName = "a null asset")]
    public async Task A_malformed_answer_is_a_typed_failure(string body)
    {
        using ReleasesBed bed = new(testContext);
        bed.Server.Answer(ReleasesBed.ApiLatest, HttpStatusCode.OK, body);

        ReleaseDataException failure = await Assert.ThrowsAsync<ReleaseDataException>(bed.Latest);

        Assert.AreEqual(new Uri(ReleasesBed.ApiLatest), failure.Url);
        Assert.HasCount(1, bed.Server.Requests, "the pages do not stand in for an answer the API gave");
    }

    [TestMethod]
    [DataRow(/*lang=json,strict*/ """{"tag_name":"v1"}""", DisplayName = "an object where a list belongs")]
    [DataRow(/*lang=json,strict*/ """[{"tag_name":"v1","assets":[]}, 7]""", DisplayName = "a number among the releases")]
    public async Task A_list_of_another_shape_is_a_typed_failure(string body)
    {
        using ReleasesBed bed = new(testContext);
        bed.Server.Answer(ReleasesBed.ApiList(5), HttpStatusCode.OK, body);

        _ = await Assert.ThrowsAsync<ReleaseDataException>(() => bed.List(5));
    }

    [TestMethod]
    public async Task A_malformed_answer_is_not_replaced_by_a_kept_one()
    {
        using ReleasesBed bed = new(testContext);
        bed.Server.Answer(ReleasesBed.ApiLatest, HttpStatusCode.OK, ReleaseFixtures.Fixture("dxvk-latest.json"))
            .Answer(ReleasesBed.ApiLatest, HttpStatusCode.OK, "{ cut short");

        _ = await bed.Latest();
        bed.Net.Advance(7 * 3600);
        _ = await Assert.ThrowsAsync<ReleaseDataException>(bed.Latest);
    }

    [TestMethod]
    public async Task Missing_flags_and_date_are_not_known()
    {
        using ReleasesBed bed = new(testContext);
        bed.Server.Answer(ReleasesBed.ApiLatest, HttpStatusCode.OK, /*lang=json,strict*/ """{"tag_name":"v1","published_at":null,"extra":{"nested":[1,2]}}""");

        ReleaseAnswer? answer = await bed.Latest();

        Assert.IsNotNull(answer);
        Assert.IsNull(answer.Release.Prerelease);
        Assert.IsNull(answer.Release.Draft);
        Assert.IsNull(answer.Release.PublishedAt);
        Assert.IsEmpty(answer.Release.Assets);
    }
}
