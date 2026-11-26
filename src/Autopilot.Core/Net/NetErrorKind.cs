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

    /// <summary>TLS refused the server's certificate; <see cref="NetException.Certificate"/> says why.</summary>
    Certificate,

    /// <summary>The server asked to wait; <see cref="NetException.RetryAt"/> says until when, when known.</summary>
    RateLimited,

    /// <summary>A redirect hop was refused; <see cref="NetException.Redirect"/> says why.</summary>
    RedirectRefused,

    /// <summary>The URL's host is not on the caller's list.</summary>
    HostNotAllowed,

    /// <summary>The URL is not https.</summary>
    SchemeRefused,

    /// <summary>The answer is larger than its limit.</summary>
    TooLarge,

    /// <summary>The server's final answer was not a success; <see cref="NetException.StatusCode"/> holds it.</summary>
    HttpStatus,
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
