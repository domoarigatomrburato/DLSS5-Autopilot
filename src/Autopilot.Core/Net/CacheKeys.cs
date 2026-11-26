using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Autopilot.Core.Archives;

namespace Autopilot.Core.Net;

/// <summary>The identities of cache entries: the first 32 hex digits of a SHA-256 over what makes an entry what it is.</summary>
internal static class CacheKeys
{
    /// <summary>A download's entry: <c>&lt;root&gt;/files/&lt;key&gt;-&lt;name&gt;</c>, keyed by the URL and the expected digest.</summary>
    public static string FileEntry(string cacheRoot, Uri url, string fileName, Verification expected) =>
        Path.Combine(cacheRoot, "files", Of(url.AbsoluteUri + "\n" + (expected is Sha256Verification sha256 ? sha256.Digest : "")) + "-" + fileName);

    /// <summary>An API answer: its URL.</summary>
    public static string Api(Uri url) => Of(url.AbsoluteUri);

    /// <summary>
    /// An unpacked archive: the file it came from (its path, size and last write, since an entry
    /// fetched again under the same name may hold other bytes) and every option that shapes the
    /// tree, with case folded.
    /// </summary>
    public static string Unpacked(string archivePath, TreeOptions? options, ArchiveLimits? limits)
    {
        FileInfo archive = new(archivePath);
        return Of(string.Join(
            '\n',
            archive.FullName,
            archive.Length.ToString(CultureInfo.InvariantCulture),
            archive.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture),
            (options ?? new()).CanonicalKey(),
            (limits ?? ArchiveLimits.Default).CanonicalKey()));
    }

    private static string Of(string identity) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..32];
}
