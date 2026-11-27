using System.Net;
using Autopilot.Core.Storage;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Net;

/// <summary>Runs a download's checks: its magic bytes, then the caller's verification. A refusal is never retried.</summary>
internal sealed partial class Verifier(NetClient client, ISignatureVerifier signatures, ILogger logger)
{
    /// <summary>
    /// The verification to run: a sums file is read and becomes the SHA-256 it lists. A sums file that
    /// cannot be read refuses the download: a network that blocks one small file must not turn the check off.
    /// </summary>
    public async Task<Verification> ResolveAsync(HostAllowlist hosts, string fileName, Verification verification, CancellationToken cancellationToken)
    {
        if (verification is not SumsFileVerification sums)
        {
            return verification;
        }
        string host = sums.Url.Host;
        TextAnswer answer;
        try
        {
            answer = await client.GetTextAsync(sums.Url, hosts, new TextRequest { Cached = true }, cancellationToken);
        }
        catch (NetException failure)
        {
            throw Refused(host, fileName, VerificationProblem.SumsUnavailable, $"the sums file could not be read ({failure.Kind})", failure);
        }
        if (answer.StatusCode is < HttpStatusCode.OK or > (HttpStatusCode)299)
        {
            throw Refused(host, fileName, VerificationProblem.SumsUnavailable, $"the sums file answered HTTP {(int)answer.StatusCode}");
        }
        return SumsList.DigestFor(answer.Body, sums.ListedName ?? fileName)
            ?? throw Refused(host, fileName, VerificationProblem.NotInSums, "the sums file does not list the name exactly once");
    }

    /// <summary>
    /// The verification to look an entry up by, without any request: a sums file becomes the SHA-256
    /// it lists only from an answer already kept, at any age. Null when that answer is not kept or
    /// does not list the name exactly once.
    /// </summary>
    public async Task<Verification?> ResolveKeptAsync(HostAllowlist hosts, string fileName, Verification verification, CancellationToken cancellationToken)
    {
        if (verification is not SumsFileVerification sums)
        {
            return verification;
        }
        string? body = await client.KeptTextAsync(sums.Url, hosts, cancellationToken);
        return body is null ? null : SumsList.DigestFor(body, sums.ListedName ?? fileName);
    }

    /// <summary>
    /// Whether a finished entry may be used: its digest is part of its key, so only the magic bytes
    /// and a signer are checked again.
    /// </summary>
    public async Task<bool> IsUsableAsync(string entry, string fileName, Verification expected, CancellationToken cancellationToken) =>
        await FileContent.LooksRightAsync(entry, fileName, cancellationToken)
        && (expected is not SignerVerification signer || await signatures.IsSignedByAsync(entry, signer.Publisher, cancellationToken));

    /// <summary>Refuses a finished <c>.part</c> file that is not what its name says or fails the verification.</summary>
    public async Task CheckAsync(string part, string fileName, string host, Verification expected, CancellationToken cancellationToken)
    {
        if (!await FileContent.LooksRightAsync(part, fileName, cancellationToken))
        {
            string start = FileContent.Start(part);
            throw new NetException(NetErrorKind.WrongContent, host, $"{fileName} from {host} is not a {Path.GetExtension(fileName)} file; it starts with {start}")
            {
                Detail = new WrongContentDetail(fileName, start),
            };
        }
        switch (expected)
        {
            case Sha256Verification { Digest: var digest } when await FileDigest.Sha256Async(part, cancellationToken) != digest:
                throw Refused(host, fileName, VerificationProblem.DigestMismatch, "its SHA-256 is not the expected one");
            case SignerVerification { Publisher: var publisher } when !await signatures.IsSignedByAsync(part, publisher, cancellationToken):
                throw Refused(host, fileName, VerificationProblem.NotSigned, $"it is not signed by {publisher}");
            case NoVerification { Reason: var reason } when logger.IsEnabled(LogLevel.Warning):
                LogUnverified(logger, fileName, host, reason);
                break;
        }
    }

    private static NetException Refused(string host, string fileName, VerificationProblem problem, string detail, Exception? cause = null) =>
        new(NetErrorKind.VerificationFailed, host, $"{fileName} from {host}: {detail}", cause)
        {
            Detail = new VerificationFailedDetail(problem, fileName),
        };

    [LoggerMessage(Level = LogLevel.Warning, Message = "{FileName} from {Host} is used without verification: {Reason}")]
    private static partial void LogUnverified(ILogger logger, string fileName, string host, string reason);
}
