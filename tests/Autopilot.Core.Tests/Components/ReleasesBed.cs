using System.Globalization;
using System.Net;
using Autopilot.Core.Components;
using Autopilot.Core.Net;
using Autopilot.Core.Tests.Net;
using Microsoft.Extensions.Logging.Testing;

namespace Autopilot.Core.Tests.Components;

/// <summary><see cref="GitHubReleases"/> on the network test bed, one attempt per request, answering from <see cref="ReleaseFixtures"/>.</summary>
internal sealed class ReleasesBed : IDisposable
{
    public const string Repo = "doitsujin/dxvk";
    public const string ApiLatest = "https://api.github.com/repos/doitsujin/dxvk/releases/latest";
    public const string ApiTag = "https://api.github.com/repos/doitsujin/dxvk/releases/tags/v3.2";
    public const string LatestPage = "https://github.com/doitsujin/dxvk/releases/latest";

    private readonly CancellationToken _cancellation;

    public ReleasesBed(TestContext context, NetOptions? options = null)
    {
        _cancellation = context.CancellationToken;
        Net = new(context, options ?? TestBed.SingleAttempt);
        Releases = new(Net.Client, Net.Time, Log);
    }

    public TestBed Net { get; }

    public FakeServer Server => Net.Server;

    public FakeLogger<GitHubReleases> Log { get; } = new();

    public GitHubReleases Releases { get; }

    public static string ApiList(int count) => $"https://api.github.com/repos/doitsujin/dxvk/releases?per_page={count}";

    public static string ListPage(int page) => $"https://github.com/doitsujin/dxvk/releases?page={page}";

    public static string Expanded(string tag) => "https://github.com/doitsujin/dxvk/releases/expanded_assets/" + tag;

    /// <summary>The API answers a primary rate limit: 403 with no requests left until an hour from now.</summary>
    public ReleasesBed RateLimited(string url)
    {
        string reset = Net.Time.GetUtcNow().AddHours(1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        Server.Answer(
            url,
            HttpStatusCode.Forbidden,
            ReleaseFixtures.Fixture("rate-limited-403.json"),
            ("x-ratelimit-limit", "60"),
            ("x-ratelimit-remaining", "0"),
            ("x-ratelimit-used", "60"),
            ("x-ratelimit-reset", reset),
            ("x-ratelimit-resource", "core"));
        return this;
    }

    /// <summary>The API answers a secondary rate limit: 429 with a <c>Retry-After</c>.</summary>
    public ReleasesBed SecondaryRateLimited(string url)
    {
        Server.Answer(url, HttpStatusCode.TooManyRequests, ReleaseFixtures.Fixture("rate-limited-429.json"), ("Retry-After", "60"));
        return this;
    }

    public ReleasesBed Unreachable(string url)
    {
        Server.Throw(url, new HttpRequestException(HttpRequestError.ConnectionError, "No route to host"));
        return this;
    }

    /// <summary>The captured list page as page 1, and an asset fragment for each of its tags.</summary>
    public ReleasesBed Pages()
    {
        Server.Answer(ListPage(1), HttpStatusCode.OK, ReleaseFixtures.Fixture("dxvk-releases-page.html"));
        return Fragments(ReleaseFixtures.PageTags);
    }

    public ReleasesBed Fragments(params IEnumerable<string> tags)
    {
        foreach (string tag in tags)
        {
            Server.Answer(Expanded(tag), HttpStatusCode.OK, ReleaseFixtures.ExpandedFor(tag));
        }

        return this;
    }

    public Task<ReleaseListAnswer> List(int count) => Releases.GetListAsync(Repo, count, _cancellation);

    public Task<ReleaseAnswer?> Latest() => Releases.GetLatestAsync(Repo, _cancellation);

    public Task<ReleaseAnswer?> ByTag(string tag) => Releases.GetByTagAsync(Repo, tag, _cancellation);

    public int RequestsTo(string url) => Server.RequestsTo(url);

    /// <summary>The methods of the requests to <paramref name="url"/>, in order.</summary>
    public HttpMethod[] MethodsTo(string url) => [.. Server.Requests.Where(request => request.Url == new Uri(url)).Select(request => request.Method)];

    /// <summary>How many asset pages were asked for.</summary>
    public int AssetPageRequests => Server.Requests.Count(request => request.Url.AbsolutePath.Contains("/expanded_assets/", StringComparison.Ordinal));

    public int RequestsToApi => Server.Requests.Count(request => request.Url.Host == "api.github.com");

    public void Dispose() => Net.Dispose();
}
