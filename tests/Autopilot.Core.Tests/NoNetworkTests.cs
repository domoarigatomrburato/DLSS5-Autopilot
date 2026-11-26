namespace Autopilot.Core.Tests;

[TestClass]
public sealed class NoNetworkTests(TestContext testContext)
{
    [TestMethod]
    public async Task A_request_that_reaches_for_the_network_fails_at_once()
    {
        using var client = new HttpClient();

        HttpRequestException failure = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAsync(new Uri("http://stray-request.test/"), testContext.CancellationToken));

        // Name resolution would mean the request set out for the real network.
        Assert.AreEqual(HttpRequestError.ConnectionError, failure.HttpRequestError);
    }
}
