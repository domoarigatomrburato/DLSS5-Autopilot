using System.Globalization;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Net;

/// <summary>The words a person reads for a failed request: what happened, naming the host, then what to do.</summary>
public static class NetExplanations
{
    public static string Explain(NetException failure)
    {
        // {0} is always the host, {1} the detail the text shows, {2} the file name.
        (string id, string shown, string fileName) = failure.Detail switch
        {
            CertificateRefusedDetail refused => ("NetCertificate" + refused.Problem, "", ""),
            RedirectRefusedDetail refused => ("NetRedirectRefused" + refused.Problem, "", ""),
            HttpStatusDetail status => ("NetHttpStatus", Number((int)status.Status), ""),
            RateLimitedDetail { RetryAt: { } at } => ("NetRateLimitedUntil", LocalTime(at), ""),
            WrongContentDetail wrong => (IsPage(wrong.FileStart) ? "NetWrongContentPage" : "NetWrongContent", wrong.FileStart, wrong.FileName),
            VerificationFailedDetail failed => ("NetVerificationFailed" + failed.Problem, "", failed.FileName),
            _ => (WithoutDetail(failure.Kind), "", ""),
        };
        return Texts.Format(id, failure.Host, shown, fileName);
    }

    // The start is shown escaped, so a byte order mark and blank lines before a page's '<' read as
    // these sequences.
    private static readonly string[] _beforeAPage = [@"\xef\xbb\xbf", " ", @"\x09", @"\x0a", @"\x0d"];

    private static bool IsPage(string fileStart)
    {
        int at = 0;
        while (Array.Find(_beforeAPage, prefix => fileStart.AsSpan(at).StartsWith(prefix, StringComparison.Ordinal)) is { } skipped)
        {
            at += skipped.Length;
        }
        return fileStart.AsSpan(at).StartsWith('<');
    }

    private static string Number(int value) => value.ToString(CultureInfo.CurrentCulture);

    private static string LocalTime(DateTimeOffset at) =>
        TimeZoneInfo.ConvertTime(at, TimeZoneInfo.Local).ToString("t", CultureInfo.CurrentCulture);

    // A kind whose texts differ by reason falls back to its most general one.
    private static string WithoutDetail(NetErrorKind kind) =>
        "Net" + kind + kind switch
        {
            NetErrorKind.Certificate => nameof(CertificateProblem.Unknown),
            NetErrorKind.RedirectRefused => nameof(RedirectProblem.Host),
            NetErrorKind.VerificationFailed => nameof(VerificationProblem.DigestMismatch),
            _ => "",
        };
}
