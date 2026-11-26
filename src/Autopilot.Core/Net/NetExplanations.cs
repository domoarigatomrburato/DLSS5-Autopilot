using System.Globalization;
using System.Resources;

namespace Autopilot.Core.Net;

/// <summary>The words a person reads for a failed request: what happened, naming the host, then what to do.</summary>
public static class NetExplanations
{
    // Looked up by id (kind, then reason), so a typed accessor per string would add nothing.
    private static readonly ResourceManager _strings = new("Autopilot.Core.Resources.Strings", typeof(NetExplanations).Assembly);

    public static string Explain(NetException failure)
    {
        string id = "Net" + failure.Kind + failure.Kind switch
        {
            NetErrorKind.Certificate => (failure.Certificate ?? CertificateProblem.Unknown).ToString(),
            NetErrorKind.RedirectRefused => (failure.Redirect ?? RedirectProblem.Host).ToString(),
            NetErrorKind.RateLimited when failure.RetryAt is not null => "Until",
            _ => "",
        };
        string format = _strings.GetString(id, CultureInfo.CurrentUICulture)
            ?? throw new InvalidOperationException($"no explanation with id {id} in the resources");
        return string.Format(CultureInfo.CurrentCulture, format, failure.Host, Detail(failure));
    }

    private static string Detail(NetException failure) =>
        failure switch
        {
            { RetryAt: { } at } => TimeZoneInfo.ConvertTime(at, TimeZoneInfo.Local).ToString("t", CultureInfo.CurrentCulture),
            { StatusCode: { } status } => ((int)status).ToString(CultureInfo.CurrentCulture),
            _ => "",
        };
}
