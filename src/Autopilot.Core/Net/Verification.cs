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

    /// <summary>The check for <paramref name="digest"/>; <see langword="null"/> when it is not 64 hex digits.</summary>
    internal static Sha256Verification? TryCreate(string? digest) => IsDigest(digest) ? new(digest) : null;

    private static bool IsDigest([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? digest) =>
        digest is { Length: 64 } && digest.All(char.IsAsciiHexDigit);

    private static string Normalize(string digest) =>
        IsDigest(digest)
            ? Convert.ToHexStringLower(Convert.FromHexString(digest))
            : throw new ArgumentException($"'{digest}' is not 64 hex digits", nameof(digest));
}

/// <summary>The file must carry a valid Authenticode signature by <paramref name="Publisher"/>.</summary>
public sealed record SignerVerification(string Publisher) : Verification;

/// <summary>
/// A <c>sha256sum</c> file, fetched from <paramref name="Url"/> with the file's own hosts, must list
/// the file by its exact name exactly once, and the file's SHA-256 must match.
/// </summary>
/// <param name="Url">Where the sums file is.</param>
/// <param name="ListedName">
/// The name the sums file lists the file under, when the cache keeps it under another one (the
/// publisher's asset name); <see langword="null"/> for the download's own file name.
/// </param>
public sealed record SumsFileVerification(Uri Url, string? ListedName = null) : Verification;

/// <summary>No publisher offers a check; <paramref name="Reason"/> is logged. The magic-byte check still runs.</summary>
public sealed record NoVerification(string Reason) : Verification
{
    public string Reason { get; } = string.IsNullOrWhiteSpace(Reason)
        ? throw new ArgumentException("an unverified download needs a reason", nameof(Reason))
        : Reason;
}

/// <summary>
/// Checks Authenticode signatures and reports what it found; the caller decides what to accept.
/// Windows only; tests pass a fake.
/// </summary>
public interface ISignatureVerifier
{
    /// <summary>
    /// Whether Windows trusts the signature of the file at <paramref name="path"/>, and who signed it.
    /// A file that is missing or cannot be opened is <see cref="SignatureStatus.Unreadable"/>, not an exception.
    /// </summary>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    Task<SignatureCheck> CheckAsync(string path, CancellationToken cancellationToken);
}

/// <summary>What a signature check found. A <see cref="SignatureStatus.Valid"/> check always names its signer: one whose signer cannot be read is <see cref="SignatureStatus.Unreadable"/>.</summary>
/// <param name="Status">Whether Windows trusts the signature.</param>
/// <param name="Signer">
/// The signing certificate's subject simple name (its CN, e.g. <c>NVIDIA Corporation</c>), whenever
/// the signature could be parsed, even an untrusted one; <see langword="null"/> when the file is not
/// signed or cannot be read.
/// </param>
/// <param name="Detail">Windows' reason, for the log: the HRESULT in hex and its message.</param>
public sealed record SignatureCheck(SignatureStatus Status, string? Signer, string? Detail);

public enum SignatureStatus
{
    /// <summary>
    /// Windows trusts the signature. A timestamped signature stays valid after its certificate
    /// expires; when revocation could not be checked, the detail says so.
    /// </summary>
    Valid,

    /// <summary>The file carries no signature, or is of a kind that cannot carry one.</summary>
    NotSigned,

    /// <summary>The signature does not match the file (a bad digest) or is malformed.</summary>
    Invalid,

    /// <summary>The chain ends at an untrusted root, is broken, is explicitly distrusted, or holds a revoked certificate.</summary>
    Untrusted,

    /// <summary>The signing certificate has expired and the signature carries no timestamp.</summary>
    Expired,

    /// <summary>The file is missing or locked, or the check itself failed.</summary>
    Unreadable,
}

/// <summary>A finished, verified download in the cache. A sums file comes back as the SHA-256 it resolved to.</summary>
public sealed record CachedFile(string Path, long Size, Verification VerifiedBy);
