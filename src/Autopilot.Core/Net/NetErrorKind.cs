namespace Autopilot.Core.Net;

public enum NetErrorKind
{
    /// <summary>A firewall or security program refused the socket.</summary>
    Blocked,

    /// <summary>The host name does not resolve.</summary>
    Dns,

    /// <summary>Nothing is listening.</summary>
    Refused,

    /// <summary>No answer in time, or no bytes for too long.</summary>
    Timeout,

    /// <summary>The connection dropped mid-transfer.</summary>
    Cut,

    /// <summary>TLS refused the server's certificate; <see cref="CertificateRefusedDetail"/> says why.</summary>
    Certificate,

    /// <summary>The server asked to wait; <see cref="RateLimitedDetail"/> says until when, when known.</summary>
    RateLimited,

    /// <summary>A redirect hop was refused; <see cref="RedirectRefusedDetail"/> says why.</summary>
    RedirectRefused,

    /// <summary>Something between this PC and the host, such as a proxy, answered in a way that is not the server's.</summary>
    BadResponse,

    /// <summary>The URL's host is not on the caller's list.</summary>
    HostNotAllowed,

    /// <summary>The URL is not https.</summary>
    SchemeRefused,

    /// <summary>The answer is larger than its limit.</summary>
    TooLarge,

    /// <summary>The server's final answer was not a success; <see cref="HttpStatusDetail"/> holds it.</summary>
    HttpStatus,

    /// <summary>The file does not start the way its name says; <see cref="WrongContentDetail"/> shows how it starts.</summary>
    WrongContent,

    /// <summary>The file failed the caller's verification; <see cref="VerificationFailedDetail"/> says why.</summary>
    VerificationFailed,
}

public enum VerificationProblem
{
    /// <summary>The file's SHA-256 is not the expected one.</summary>
    DigestMismatch,

    /// <summary>The file is not signed by the expected publisher.</summary>
    NotSigned,

    /// <summary>The sums file could not be read.</summary>
    SumsUnavailable,

    /// <summary>The sums file does not list the file's name exactly once.</summary>
    NotInSums,
}

public enum CertificateProblem
{
    Unknown,

    /// <summary>The certificate was issued to another name.</summary>
    NameMismatch,

    /// <summary>The certificate is outside its dates, usually because of the PC's clock.</summary>
    Clock,

    /// <summary>An untrusted root, usually an antivirus or proxy inspecting HTTPS.</summary>
    Interception,

    /// <summary>The chain could not be built up to a root in the store.</summary>
    MissingRoot,
}

public enum RedirectProblem
{
    /// <summary>The hop leaves https.</summary>
    Scheme,

    /// <summary>The hop goes to a host off the caller's list.</summary>
    Host,

    /// <summary>More hops than the limit, usually a loop.</summary>
    TooManyHops,
}
