using System.Net;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Net;

[TestClass]
public sealed class LoggingTests(TestContext testContext)
{
    [TestMethod]
    public async Task Retries_and_failures_are_logged_by_host_without_the_query_that_can_hold_a_signed_token()
    {
        const string signed = "https://cdn.example.test/asset.zip?sig=secret-token";
        using TestBed bed = new(testContext);
        bed.Server.Answer(signed, HttpStatusCode.BadGateway);

        Task<NetException> pending = bed.GetTextFails(signed);
        bed.Advance(1, 4);
        _ = await pending;

        string[] lines = bed.LogLines;
        Assert.HasCount(3, lines, "two retries and the failure");
        foreach (string line in lines)
        {
            Assert.Contains("cdn.example.test/asset.zip", line);
            Assert.DoesNotContain("secret-token", line);
        }
    }
}
