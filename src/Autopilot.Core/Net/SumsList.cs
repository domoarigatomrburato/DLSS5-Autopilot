namespace Autopilot.Core.Net;

/// <summary>
/// Reads a <c>sha256sum</c> file: one <c>&lt;64 hex&gt; &lt;space or *&gt;&lt;name&gt;</c> per line, or
/// <c>&lt;64 hex&gt; &lt;name&gt;</c> with a single space, as some publishers write it.
/// </summary>
internal static class SumsList
{
    /// <summary>The digest listed for exactly <paramref name="fileName"/>, or null when it is listed none or several times.</summary>
    public static Sha256Verification? DigestFor(string body, string fileName)
    {
        Sha256Verification[] digests =
        [
            .. body.Split('\n')
                .Select(line => line.TrimEnd('\r'))
                .Where(line => line.Length > 65 && line[64] == ' ' && NameOf(line) == fileName)
                .Select(line => Sha256Verification.TryCreate(line[..64]))
                .OfType<Sha256Verification>(),
        ];
        return digests.Length == 1 ? digests[0] : null;
    }

    // After the digest's space: a second space or '*' marks text or binary mode, otherwise the name starts at once.
    private static string NameOf(string line) => line[65] is ' ' or '*' ? line[66..] : line[65..];
}
