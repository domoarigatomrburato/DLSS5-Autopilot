using System.Net;

namespace Autopilot.Core.Net;

/// <summary>What a <see cref="NetException"/> knows beyond its kind. The records below are the only ones.</summary>
public abstract record NetFailureDetail
{
    internal NetFailureDetail()
    {
    }
}

/// <summary>Why TLS refused the server's certificate.</summary>
public sealed record CertificateRefusedDetail(CertificateProblem Problem) : NetFailureDetail;

/// <summary>Why a redirect was refused, at which hop, counting from 1.</summary>
public sealed record RedirectRefusedDetail(RedirectProblem Problem, int Hop) : NetFailureDetail;

/// <summary>The server's final status, which was not a success.</summary>
public sealed record HttpStatusDetail(HttpStatusCode Status) : NetFailureDetail;

/// <summary>When the server will answer again, when it said so, and the status it refused with, when there was one.</summary>
public sealed record RateLimitedDetail(DateTimeOffset? RetryAt, HttpStatusCode? Status) : NetFailureDetail;

/// <summary>The downloaded file that does not start the way its name says, and its first bytes with anything unprintable escaped.</summary>
public sealed record WrongContentDetail(string FileName, string FileStart) : NetFailureDetail;

/// <summary>Why the downloaded file failed the caller's verification.</summary>
public sealed record VerificationFailedDetail(VerificationProblem Problem, string FileName) : NetFailureDetail;
