using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.GitHub;
using Autopilot.Core.Net;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Components.Sources;

/// <summary>
/// The components outside the mirror, NVIDIA's runtimes, ReShade, the feeder and OptiScaler, each
/// resolved by its catalog rule once per run: a success is reused for the rest of the run, a failure
/// is asked again next time.
/// </summary>
public sealed partial class ComponentSources
{
    private readonly ResolutionMemory<string, ComponentRelease> _memory;
    private readonly Dictionary<string, Func<CancellationToken, Task<ComponentRelease>>> _resolvers;
    private readonly BranchSnapshots _branches;
    private readonly ILogger<ComponentSources> _logger;

    /// <summary>Built by a <see cref="ComponentRun"/>, whose token (<paramref name="run"/>) its shared resolutions run under.</summary>
    internal ComponentSources(GitHubReleases releases, NetClient client, TimeProvider time, ILogger<ComponentSources> logger, CancellationToken run)
    {
        _memory = new(run);
        ReleaseComponents fromReleases = new(releases);
        ListedComponents listed = new(releases);
        StandaloneComponent standalone = new(releases, client);
        _branches = new(client, time, logger, run);
        _logger = logger;
        _resolvers = new(StringComparer.Ordinal)
        {
            [ComponentIds.Bridge] = fromReleases.BridgeAsync,
            [ComponentIds.Upstream] = fromReleases.UpstreamAsync,
            [ComponentIds.Standalone] = standalone.ResolveAsync,
            [ComponentIds.Uplift] = listed.UpliftAsync,
            [ComponentIds.Dxvk] = fromReleases.DxvkAsync,
            [ComponentIds.Reframework] = fromReleases.ReframeworkAsync,
            [ComponentIds.Mfg] = listed.MfgAsync,
            [ComponentIds.AsiLoader] = listed.AsiLoaderAsync,
            [ComponentIds.RemixRuntime] = listed.RemixRuntimeAsync,
            [ComponentIds.Vort] = _branches.VortAsync,
            [ComponentIds.ShaderHeaders] = _branches.HeadersAsync,
        };
    }

    /// <summary>The ids <see cref="ResolveAsync"/> takes; LumeniteFX has its own method.</summary>
    internal IReadOnlyCollection<string> Ids => _resolvers.Keys;

    /// <summary>The component <paramref name="id"/>, resolved by its rule; the same answer for the rest of the run once found.</summary>
    /// <exception cref="ArgumentException"><paramref name="id"/> is not a component this method resolves (LumeniteFX has its own).</exception>
    /// <exception cref="ComponentException">No release carries the component, its layout changed, its sums are missing, or its branch could not be resolved.</exception>
    /// <exception cref="NetException">Neither GitHub's API, nor a kept answer, nor the pages could answer, and the component has no last resort.</exception>
    /// <exception cref="ReleaseDataException">The API's answer is not release data.</exception>
    public Task<ComponentRelease> ResolveAsync(string id, CancellationToken cancellationToken)
    {
        Func<CancellationToken, Task<ComponentRelease>> resolve = _resolvers.GetValueOrDefault(id)
            ?? throw new ArgumentException($"'{id}' is not resolved here", nameof(id));
        return _memory.GetAsync(id, token => LoggedAsync(resolve(token)), cancellationToken);
    }

    /// <summary>LumeniteFX at its newest commit, with the files of its one provider, <see cref="MotionProvider.LumeniteKernel"/>.</summary>
    /// <exception cref="ComponentException">The branch could not be resolved to a commit.</exception>
    /// <exception cref="ReleaseDataException">The API's answer names no commit.</exception>
    public Task<ComponentRelease> ResolveLumeniteAsync(CancellationToken cancellationToken) =>
        _memory.GetAsync(ComponentIds.Lumenite, token => LoggedAsync(_branches.LumeniteAsync(token)), cancellationToken);

    private async Task<ComponentRelease> LoggedAsync(Task<ComponentRelease> resolving)
    {
        ComponentRelease release = await resolving;
        if (_logger.IsEnabled(LogLevel.Information))
        {
            LogResolved(_logger, release.Id, release.Version.Label, release.Downloads.Count);
        }

        return release;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Id} {Version}: {Downloads} download(s)")]
    private static partial void LogResolved(ILogger logger, string id, string version, int downloads);
}
