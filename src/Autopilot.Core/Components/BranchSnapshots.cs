using System.Net;
using System.Text.Json;
using Autopilot.Core.Net;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Components;

/// <summary>
/// The components published as a branch rather than as releases: LumeniteFX's <c>mainline</c>,
/// VORT's <c>main</c>, ReShade's <c>slim</c> shader headers. Each branch is resolved to its head
/// commit by the API (kept like any API answer), that commit's files are downloaded and cached by
/// commit, and the commit is the version recorded. Without a commit nothing is downloaded: a moving
/// branch is never installed with no record of what it was.
/// </summary>
internal sealed class BranchSnapshots(NetClient client, TimeProvider time)
{
    /// <summary>The four includes every LumeniteFX provider needs.</summary>
    public static readonly string[] LumeniteIncludes =
        ["lumenite_Projections.fxh", "lumenite_Helpers.fxh", "lumenite_Compute.fxh", "lumenite_ColorManagement.fxh"];

    /// <summary>The headers the feeder's and the standalone add-on's shaders include.</summary>
    public static readonly string[] Headers = ["ReShade.fxh", "ReShadeUI.fxh", "DrawText.fxh"];

    private const int CommitLength = 40;

    private static readonly TextRequest _apiRequest = new() { Cached = true };

    /// <summary>LumeniteFX at the head of <c>mainline</c>: <paramref name="shader"/>, its includes and its texture.</summary>
    public Task<ComponentRelease> LumeniteAsync(string shader, CancellationToken cancellationToken) =>
        ArchiveAsync(
            ComponentIds.Lumenite,
            ComponentRepositories.Lumenite,
            "mainline",
            [new("Shaders/" + shader), .. LumeniteIncludes.Select(include => new ExpectedFile(include)), new("Textures/lumenite_bluenoise256.png")],
            cancellationToken);

    /// <summary>VORT at the head of <c>main</c>: <c>vort_Motion.fx</c>, its includes under <c>Shaders/Includes</c>, its texture.</summary>
    public Task<ComponentRelease> VortAsync(CancellationToken cancellationToken) =>
        ArchiveAsync(
            ComponentIds.Vort,
            ComponentRepositories.Vort,
            "main",
            [new("Shaders/vort_Motion.fx"), new("Textures/vort_BlueNoise.png")],
            cancellationToken);

    /// <summary>The three shader headers at the head of <c>slim</c>, as raw files of that commit.</summary>
    public async Task<ComponentRelease> HeadersAsync(CancellationToken cancellationToken)
    {
        var repo = GitHubRepository.Parse(ComponentRepositories.ShaderHeaders);
        (string commit, ReleaseSource source) = await HeadAsync(repo, "slim", cancellationToken);
        NoVerification check = ComponentCatalog.Unchecked(ComponentIds.ShaderHeaders);
        return new(
            ComponentIds.ShaderHeaders,
            new(commit),
            [.. Headers.Select(name => new ComponentDownload(name, repo.RawFile(commit, "Shaders/" + name), check))],
            [.. Headers.Select(name => new ExpectedFile(name))],
            source);
    }

    private async Task<ComponentRelease> ArchiveAsync(string id, string repository, string branch, IReadOnlyList<ExpectedFile> files, CancellationToken cancellationToken)
    {
        var repo = GitHubRepository.Parse(repository);
        (string commit, ReleaseSource source) = await HeadAsync(repo, branch, cancellationToken);
        ComponentDownload archive = new($"{repo.Name}-{commit}.zip", repo.CommitArchive(commit), ComponentCatalog.Unchecked(id));
        return new(id, new(commit), [archive], files, source);
    }

    // The branch's head commit from the API, fresh or kept from an earlier run; a refusal is never covered.
    private async Task<(string Commit, ReleaseSource Source)> HeadAsync(GitHubRepository repo, string branch, CancellationToken cancellationToken)
    {
        Uri api = repo.BranchHead(branch);
        TextAnswer answer;
        try
        {
            answer = await client.GetTextAsync(api, ComponentHosts.Allowlist, _apiRequest, cancellationToken);
        }
        catch (NetException failure) when (!failure.IsRefusal)
        {
            throw Unavailable(repo, branch, failure.Message, failure);
        }

        if (answer.StatusCode != HttpStatusCode.OK)
        {
            throw Unavailable(repo, branch, $"HTTP {(int)answer.StatusCode}", null);
        }

        TimeSpan age = time.GetUtcNow() - answer.FetchedAt;
        return (CommitOf(api, answer.Body), new(answer.IsStale ? ReleaseOrigin.StaleCache : ReleaseOrigin.Api, age < TimeSpan.Zero ? TimeSpan.Zero : age));
    }

    private static ComponentException Unavailable(GitHubRepository repo, string branch, string why, Exception? cause) =>
        new(ComponentFailure.BranchUnavailable, Texts.Format("ComponentBranchUnavailable", repo, branch, why), cause);

    private static string CommitOf(Uri api, string body)
    {
        string? commit;
        try
        {
            using var document = JsonDocument.Parse(body);
            commit = document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("sha", out JsonElement sha)
                && sha.ValueKind == JsonValueKind.String
                ? sha.GetString()
                : null;
        }
        catch (JsonException failure)
        {
            throw new ReleaseDataException(api, $"{api.AbsolutePath}: the answer is not JSON", failure);
        }

        return commit is { Length: CommitLength } && commit.All(char.IsAsciiHexDigitLower)
            ? commit
            : throw new ReleaseDataException(api, $"{api.AbsolutePath}: the answer names no commit");
    }
}
