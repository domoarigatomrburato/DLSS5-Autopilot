using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Autopilot.Core.Net;
using Autopilot.Core.Storage;

namespace Autopilot.Core.Components;

/// <summary>
/// Release data from the API's JSON, reading only the fields this tool needs. A malformed answer is
/// a <see cref="ReleaseDataException"/>, never a crash.
/// </summary>
internal static class ReleaseJson
{
    private const string DigestPrefix = "sha256:";

    private static readonly SearchValues<char> _hexDigits = SearchValues.Create("0123456789abcdefABCDEF");

    /// <exception cref="ReleaseDataException">The answer is not one release.</exception>
    public static GitHubRelease One(Uri url, string body)
    {
        using JsonDocument document = Parse(url, body);
        return Release(url, document.RootElement);
    }

    /// <exception cref="ReleaseDataException">The answer is not a list of releases.</exception>
    public static IReadOnlyList<GitHubRelease> List(Uri url, string body)
    {
        using JsonDocument document = Parse(url, body);
        return document.RootElement.ValueKind == JsonValueKind.Array
            ? [.. document.RootElement.EnumerateArray().Select(release => Release(url, release))]
            : throw Malformed(url, "the answer is not a list");
    }

    private static JsonDocument Parse(Uri url, string body)
    {
        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException failure)
        {
            throw Malformed(url, failure.Message, failure);
        }
    }

    private static GitHubRelease Release(Uri url, JsonElement release)
    {
        if (UntrustedJson.Text(release, "tag_name") is not { Length: > 0 } tag)
        {
            throw Malformed(url, "a release without a tag");
        }

        return new(
            tag,
            UntrustedJson.Flag(release, "prerelease"),
            UntrustedJson.Flag(release, "draft"),
            PublishedAt(UntrustedJson.Text(release, "published_at")),
            [.. UntrustedJson.Array(release, "assets").Select(asset => Asset(url, tag, asset))]);
    }

    private static GitHubAsset Asset(Uri url, string tag, JsonElement asset) =>
        UntrustedJson.Text(asset, "name") is { Length: > 0 } name
        && Uri.TryCreate(UntrustedJson.Text(asset, "browser_download_url"), UriKind.Absolute, out Uri? download)
            ? new(name, download, Digest(UntrustedJson.Text(asset, "digest")))
            : throw Malformed(url, $"release {tag} has an asset without a name or a download address");

    private static DateTimeOffset? PublishedAt(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset at) ? at : null;

    // GitHub names the algorithm; only a SHA-256 is a check this tool can run.
    private static Sha256Verification? Digest(string? digest) =>
        digest is not null
        && digest.StartsWith(DigestPrefix, StringComparison.OrdinalIgnoreCase)
        && digest.Length == DigestPrefix.Length + 64
        && !digest.AsSpan(DigestPrefix.Length).ContainsAnyExcept(_hexDigits)
            ? new(digest[DigestPrefix.Length..])
            : null;

    private static ReleaseDataException Malformed(Uri url, string reason, Exception? inner = null) =>
        new(url, $"{url.GetLeftPart(UriPartial.Path)} is not release data: {reason}", inner);
}
