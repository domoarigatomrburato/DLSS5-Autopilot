using System.Net;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Net;

[TestClass]
public sealed class ExplanationTests
{
    private const string Host = "downloads.example.test";

    public static IEnumerable<object[]> EveryFailure()
    {
        foreach (NetErrorKind kind in Enum.GetValues<NetErrorKind>())
        {
            yield return [new NetException(kind, Host, "technical")];
        }

        foreach (CertificateProblem problem in Enum.GetValues<CertificateProblem>())
        {
            yield return [new NetException(NetErrorKind.Certificate, Host, "technical") { Certificate = problem }];
        }

        foreach (RedirectProblem problem in Enum.GetValues<RedirectProblem>())
        {
            yield return [new NetException(NetErrorKind.RedirectRefused, Host, "technical") { Redirect = problem, Hop = 2 }];
        }

        yield return [new NetException(NetErrorKind.RateLimited, Host, "technical") { RetryAt = new DateTimeOffset(2026, 3, 1, 13, 0, 0, TimeSpan.Zero) }];
        yield return [new NetException(NetErrorKind.HttpStatus, Host, "technical") { StatusCode = HttpStatusCode.BadGateway }];
    }

    [TestMethod]
    [DynamicData(nameof(EveryFailure))]
    public void Every_failure_is_explained_in_words_naming_the_host(NetException failure)
    {
        string explanation = NetExplanations.Explain(failure);

        StringAssert.Contains(explanation, Host, StringComparison.Ordinal);
        Assert.DoesNotContain("technical", explanation, "the explanation comes from resources, not the log line");
        Assert.IsTrue(char.IsUpper(explanation[0]), "sentence case");
        Assert.AreEqual('.', explanation[^1], "ends with what to do, as a sentence");
    }

    [TestMethod]
    public void Each_kind_has_its_own_explanation()
    {
        string[] explanations = [.. Enum.GetValues<NetErrorKind>().Select(kind => NetExplanations.Explain(new NetException(kind, Host, "x")))];

        Assert.HasCount(explanations.Length, explanations.Distinct());
    }

    [TestMethod]
    public void Each_certificate_reason_has_its_own_explanation()
    {
        string[] explanations =
        [
            .. Enum.GetValues<CertificateProblem>().Select(problem =>
                NetExplanations.Explain(new NetException(NetErrorKind.Certificate, Host, "x") { Certificate = problem })),
        ];

        Assert.HasCount(explanations.Length, explanations.Distinct());
    }

    [TestMethod]
    public void A_failed_status_names_its_code()
    {
        string explanation = NetExplanations.Explain(new NetException(NetErrorKind.HttpStatus, Host, "x") { StatusCode = HttpStatusCode.BadGateway });

        StringAssert.Contains(explanation, "502", StringComparison.Ordinal);
    }
}
