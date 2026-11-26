namespace Autopilot.Core.Net;

/// <summary>How a download proves it is the publisher's file before it enters the cache. The kinds below are the only ones.</summary>
public abstract record Verification
{
    internal Verification()
    {
    }
}

/// <summary>The file's SHA-256 must equal <paramref name="Digest"/>: 64 hex digits, any case, kept in lowercase.</summary>
public sealed record Sha256Verification(string Digest) : Verification
{
    public string Digest { get; } = Normalize(Digest);

    private static string Normalize(string digest) =>
        digest.Length == 64 && digest.All(char.IsAsciiHexDigit)
            ? Convert.ToHexStringLower(Convert.FromHexString(digest))
            : throw new ArgumentException($"'{digest}' is not 64 hex digits", nameof(digest));
}

/// <summary>The file must carry a valid Authenticode signature by <paramref name="Publisher"/>.</summary>
public sealed record SignerVerification(string Publisher) : Verification;

/// <summary>
/// A <c>sha256sum</c> file, fetched from <paramref name="Url"/> with the file's own hosts, must list
/// the file by its exact name exactly once, and the file's SHA-256 must match.
/// </summary>
public sealed record SumsFileVerification(Uri Url) : Verification;

/// <summary>No publisher offers a check; <paramref name="Reason"/> is logged. The magic-byte check still runs.</summary>
public sealed record NoVerification(string Reason) : Verification
{
    public string Reason { get; } = string.IsNullOrWhiteSpace(Reason)
        ? throw new ArgumentException("an unverified download needs a reason", nameof(Reason))
        : Reason;
}

/// <summary>Checks Authenticode signatures. Windows only; tests pass a fake.</summary>
public interface ISignatureVerifier
{
    /// <summary>Whether the file carries a valid signature whose signer is <paramref name="publisher"/>.</summary>
    Task<bool> IsSignedByAsync(string path, string publisher, CancellationToken cancellationToken);
}

/// <summary>A finished, verified download in the cache. A sums file comes back as the SHA-256 it resolved to.</summary>
public sealed record CachedFile(string Path, long Size, Verification VerifiedBy);
