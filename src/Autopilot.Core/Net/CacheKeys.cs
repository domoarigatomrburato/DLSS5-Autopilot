using System.Buffers;
using System.Globalization;
using System.IO.Enumeration;
using System.Security.Cryptography;
using System.Text;
using Autopilot.Core.Archives;

namespace Autopilot.Core.Net;

/// <summary>The identities of cache entries: the first 32 hex digits of a SHA-256 over what makes an entry what it is.</summary>
internal static class CacheKeys
{
    private const int KeyLength = 32;

    private static readonly SearchValues<char> _lowerHex = SearchValues.Create("0123456789abcdef");

    // What a download's entry is while it is written, and its lock: neither is a kept download.
    private static readonly string[] _notKept = [PartDownload.PartOf(""), CacheLock.PathFor("")];

    /// <summary>A download's entry: <c>&lt;root&gt;/files/&lt;key&gt;-&lt;name&gt;</c>, keyed by the URL and the expected digest.</summary>
    public static string FileEntry(string cacheRoot, Uri url, string fileName, Verification expected) =>
        Path.Combine(FilesFolder(cacheRoot), Of(url.AbsoluteUri + "\n" + (expected is Sha256Verification sha256 ? sha256.Digest : "")) + "-" + fileName);

    /// <summary>The readable names of the kept downloads under <paramref name="cacheRoot"/> matching <paramref name="searchPattern"/>, ignoring case.</summary>
    public static IReadOnlyList<string> KeptNames(string cacheRoot, string searchPattern)
    {
        string folder = FilesFolder(cacheRoot);
        return Directory.Exists(folder)
            ? [.. Directory.EnumerateFiles(folder)
                .Select(path => ReadableName(Path.GetFileName(path)))
                .OfType<string>()
                .Where(name => FileSystemName.MatchesSimpleExpression(searchPattern, name, ignoreCase: true))
                .Order(StringComparer.Ordinal)]
            : [];
    }

    /// <summary>
    /// Whether <paramref name="fullPath"/>, an absolute path, is a download's entry directly in
    /// <paramref name="cacheRoot"/>'s files folder: not a lock, a <c>.part</c> or anything elsewhere.
    /// </summary>
    public static bool IsFileEntry(string cacheRoot, string fullPath)
    {
        string folder = Path.GetFullPath(FilesFolder(cacheRoot));
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(Path.GetDirectoryName(fullPath), Path.TrimEndingDirectorySeparator(folder), comparison)
            && ReadableName(Path.GetFileName(fullPath)) is not null;
    }

    // The name a download's entry carries after its key; null for anything else in the folder: a
    // lock, an unfinished .part, a file of another shape.
    private static string? ReadableName(string entryName) =>
        entryName.Length > KeyLength + 1
        && entryName[KeyLength] == '-'
        && !entryName.AsSpan(0, KeyLength).ContainsAnyExcept(_lowerHex)
        && !Array.Exists(_notKept, suffix => entryName.EndsWith(suffix, StringComparison.Ordinal))
            ? entryName[(KeyLength + 1)..]
            : null;

    // Where the downloads are kept.
    private static string FilesFolder(string cacheRoot) => Path.Combine(cacheRoot, "files");

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
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..KeyLength];
}
