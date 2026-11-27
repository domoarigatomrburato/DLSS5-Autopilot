using System.Net;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.GitHub;
using Autopilot.Core.Net;
using Autopilot.Core.Resources;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Components.Sources;

/// <summary>
/// The components published as a branch rather than as releases: LumeniteFX's <c>mainline</c>,
/// VORT's <c>main</c>, ReShade's <c>slim</c> shader headers. Each branch is resolved to its head
/// commit by the API (kept like any API answer), that commit's files are downloaded and cached by
/// commit, and the commit is the version recorded. Without a commit nothing is downloaded: a moving
/// branch is never installed with no record of what it was.
/// </summary>
internal sealed class BranchSnapshots(NetClient client, TimeProvider time, ILogger logger)
{
    /// <summary>The four includes LumeniteFX's kernel needs.</summary>
    public static readonly string[] LumeniteIncludes =
        ["lumenite_Projections.fxh", "lumenite_Helpers.fxh", "lumenite_Compute.fxh", "lumenite_ColorManagement.fxh"];

    /// <summary>The headers the feeder's and the standalone add-on's shaders include.</summary>
    public static readonly string[] Headers = ["ReShade.fxh", "ReShadeUI.fxh", "DrawText.fxh"];

    // One head per repository and branch for the run.
    private readonly ResolutionMemory<(GitHubRepository Repo, string Branch), (string Commit, ReleaseSource Source)> _heads = new();

    /// <summary>LumeniteFX at the head of <c>mainline</c>: <c>lumenite_Kernel.fx</c>, its includes and its texture.</summary>
    public Task<ComponentRelease> LumeniteAsync(CancellationToken cancellationToken) =>
        ArchiveAsync(
            ComponentIds.Lumenite,
            "mainline",
            [new("Shaders/lumenite_Kernel.fx"), .. LumeniteIncludes.Select(include => new ExpectedFile(include)), new("Textures/lumenite_bluenoise256.png")],
            cancellationToken);

    /// <summary>VORT at the head of <c>main</c>: <c>vort_Motion.fx</c>, its includes under <c>Shaders/Includes</c>, its texture.</summary>
    public Task<ComponentRelease> VortAsync(CancellationToken cancellationToken) =>
        ArchiveAsync(
            ComponentIds.Vort,
            "main",
            [new("Shaders/vort_Motion.fx"), new("Textures/vort_BlueNoise.png")],
            cancellationToken);

    /// <summary>The three shader headers at the head of <c>slim</c>, as raw files of that commit.</summary>
    public async Task<ComponentRelease> HeadersAsync(CancellationToken cancellationToken)
    {
        CatalogEntry entry = ComponentCatalog.Of(ComponentIds.ShaderHeaders);
        GitHubRepository repo = entry.Repository;
        (string commit, ReleaseSource source) = await HeadAsync(repo, "slim", cancellationToken);
        NoVerification check = entry.Unchecked();
        return new(
            ComponentIds.ShaderHeaders,
            new(commit),
            [.. Headers.Select(name => new ComponentDownload(name, repo.RawFile(commit, "Shaders/" + name), check))],
            [.. Headers.Select(name => new ExpectedFile(name))],
            source);
    }

    private async Task<ComponentRelease> ArchiveAsync(string id, string branch, IReadOnlyList<ExpectedFile> files, CancellationToken cancellationToken)
    {
        CatalogEntry entry = ComponentCatalog.Of(id);
        GitHubRepository repo = entry.Repository;
        (string commit, ReleaseSource source) = await HeadAsync(repo, branch, cancellationToken);
        ComponentDownload archive = new($"{repo.Name}-{commit}.zip", repo.CommitArchive(commit), entry.Unchecked());
        return new(id, new(commit), [archive], files, source);
    }

    private Task<(string Commit, ReleaseSource Source)> HeadAsync(GitHubRepository repo, string branch, CancellationToken cancellationToken) =>
        _heads.GetAsync((repo, branch), token => ReadHeadAsync(repo, branch, token), cancellationToken);

    // The branch's head commit from the API, fresh or kept from an earlier run; a refusal is never covered.
    private async Task<(string Commit, ReleaseSource Source)> ReadHeadAsync(GitHubRepository repo, string branch, CancellationToken cancellationToken)
    {
        Uri api = repo.BranchHead(branch);
        TextAnswer answer;
        try
        {
            answer = await client.GetTextAsync(api, ComponentHosts.Allowlist, ReleaseResolver.ApiRequest, cancellationToken);
        }
        catch (NetException failure) when (!failure.IsRefusal)
        {
            throw Unavailable(repo, branch, failure.Message, failure);
        }

        if (answer.StatusCode != HttpStatusCode.OK)
        {
            throw Unavailable(repo, branch, $"HTTP {(int)answer.StatusCode}", null);
        }

        string commit = ReleaseJson.Commit(api, answer.Body);
        return (commit, ReleaseResolver.SourceOf(api, answer, time.GetUtcNow(), logger));
    }

    private static ComponentException Unavailable(GitHubRepository repo, string branch, string why, Exception? cause) =>
        new(ComponentFailure.BranchUnavailable, Texts.Format("ComponentBranchUnavailable", repo, branch, why), cause);
}
