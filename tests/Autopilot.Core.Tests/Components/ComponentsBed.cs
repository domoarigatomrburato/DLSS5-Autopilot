using System.Net;
using System.Text.Json.Nodes;
using Autopilot.Core.Components;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.Fetch;
using Autopilot.Core.Components.GitHub;
using Autopilot.Core.Components.Sources;
using Autopilot.Core.Net;
using Autopilot.Core.Tests.Net;
using Microsoft.Extensions.Logging.Testing;

namespace Autopilot.Core.Tests.Components;

/// <summary>
/// ReShade, the feeder, OptiScaler and the other components on the network test bed: one fake server, one cache folder,
/// the sources wired the way the composition root wires them.
/// </summary>
internal sealed class ComponentsBed : IDisposable
{
    /// <summary>The commit <c>lumenite-mainline-head.json</c> names.</summary>
    public const string Commit = "a6d42fc9851b55fcb134ab8bb1199122fafbda13";

    public static readonly string LumeniteHead = BranchHead(ComponentRepositories.Lumenite, "mainline");

    public static readonly string VortHead = BranchHead(ComponentRepositories.Vort, "main");

    public static readonly string HeadersHead = BranchHead(ComponentRepositories.ShaderHeaders, "slim");

    public ComponentsBed(TestContext context, NetOptions? options = null)
    {
        Cancellation = context.CancellationToken;
        Net = new(context, options ?? TestBed.SingleAttempt);
        Releases = new(Net.Client, Net.Time, new FakeLogger<GitHubReleases>());
        Cache = new(Net.Client, Net.CacheRoot, new FakeSigner(), Net.Time, new FakeLogger<DownloadCache>());
        ReShade = new(Net.Client, Cache, new FakeLogger<ReShadeSource>());
        Feeder = new(Releases, new FakeLogger<FeederSource>());
        OptiScaler = new(Releases, Cache, OptiScalerLog);
        Sources = new(Releases, Net.Client, Net.Time, new FakeLogger<ComponentSources>());
        Files = new(Cache);
    }

    public TestBed Net { get; }

    public FakeServer Server => Net.Server;

    public GitHubReleases Releases { get; }

    public DownloadCache Cache { get; }

    public ReShadeSource ReShade { get; }

    public FeederSource Feeder { get; }

    public FakeLogger<OptiScalerSource> OptiScalerLog { get; } = new();

    public ComponentSources Sources { get; }

    public ComponentFiles Files { get; }

    public OptiScalerSource OptiScaler { get; }

    /// <summary>What the OptiScaler source logged, in order.</summary>
    public string[] OptiScalerLines => [.. OptiScalerLog.Collector.GetSnapshot().Select(record => record.Message)];

    public CancellationToken Cancellation { get; }

    public static string Api(string repository, string rest) => $"https://api.github.com/repos/{repository}/releases{rest}";

    public static string ApiList(string repository, int count) => Api(repository, $"?per_page={count}");

    /// <summary>The API's head commit of <paramref name="branch"/> of <paramref name="repository"/>.</summary>
    public static string BranchHead(string repository, string branch) => $"https://api.github.com/repos/{repository}/commits/{branch}";

    /// <summary>github.com's release list page <paramref name="page"/> of <paramref name="repository"/>.</summary>
    public static string ListPage(string repository, int page) => $"https://github.com/{repository}/releases?page={page}";

    /// <summary>github.com's asset fragment of the release tagged <paramref name="tag"/>.</summary>
    public static string Expanded(string repository, string tag) => $"https://github.com/{repository}/releases/expanded_assets/{tag}";

    /// <summary>github.com's <c>releases/latest</c> redirect of <paramref name="repository"/>.</summary>
    public static string LatestPage(string repository) => $"https://github.com/{repository}/releases/latest";

    public static string Download(string repository, string tag, string name) => $"https://github.com/{repository}/releases/download/{tag}/{name}";

    /// <summary>Answers <paramref name="url"/> with a fixture file.</summary>
    public ComponentsBed Fixture(string url, string fixture, HttpStatusCode status = HttpStatusCode.OK)
    {
        Server.Answer(url, status, ReleaseFixtures.Fixture(fixture));
        return this;
    }

    public ComponentsBed Json(string url, JsonNode json)
    {
        Server.Answer(url, HttpStatusCode.OK, json.ToJsonString());
        return this;
    }

    /// <summary>Sources of a later run: nothing remembered, the network's cache kept.</summary>
    public ComponentSources NextRun() => new(Releases, Net.Client, Net.Time, new FakeLogger<ComponentSources>());

    /// <summary>Moves the clock on by <paramref name="hours"/>.</summary>
    public void Advance(double hours) => Net.Time.Advance(TimeSpan.FromHours(hours));

    /// <summary>Resolves <paramref name="id"/> through the sources, as an install would.</summary>
    public Task<ComponentRelease> Resolve(string id) => Sources.ResolveAsync(id, Cancellation);

    /// <summary>The API, any kept answer and github.com's pages all fail for the repository's newest release.</summary>
    public ComponentsBed NoReleaseData(string repository)
    {
        _ = Unreachable(Api(repository, "/latest"));
        return Unreachable(LatestPage(repository));
    }

    public ComponentsBed Unreachable(string url)
    {
        _ = Server.Unreachable(url);
        return this;
    }

    public void Dispose() => Net.Dispose();
}

/// <summary>Release data in the API's shape, written for the cases the captured pages do not hold.</summary>
internal static class ReleaseJsonText
{
    private static readonly DateTimeOffset _newest = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A release whose assets download from <paramref name="repository"/>; dated a day before the one listed above it.</summary>
    public static JsonObject Release(string repository, string tag, int age, params string[] assets) =>
        Release(repository, tag, prerelease: false, age, assets);

    public static JsonObject Release(string repository, string tag, bool prerelease, int age, params string[] assets) =>
        new()
        {
            ["tag_name"] = tag,
            ["draft"] = false,
            ["prerelease"] = prerelease,
            ["published_at"] = _newest.AddDays(-age).ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            ["assets"] = new JsonArray([.. assets.Select(name => (JsonNode)new JsonObject
            {
                ["name"] = name,
                ["browser_download_url"] = ComponentsBed.Download(repository, tag, name),
                ["digest"] = null,
            })]),
        };

    public static JsonArray List(params JsonObject[] releases) => [.. releases];

    /// <summary>A captured list file, to change in a test.</summary>
    public static JsonArray Captured(string fixture) => (JsonArray)JsonNode.Parse(ReleaseFixtures.Fixture(fixture))!;
}
