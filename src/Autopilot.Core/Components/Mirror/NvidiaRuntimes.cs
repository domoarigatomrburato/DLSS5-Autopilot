using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.GitHub;
using Autopilot.Core.Net;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Components.Mirror;

/// <summary>
/// NVIDIA's own <c>nvngx_dlss.dll</c>, <c>nvngx_dlssd.dll</c> and <c>nvngx_dlssg.dll</c>, read from the
/// tree of the <c>NVIDIA/DLSS</c> SDK's newest tag. The tag comes from the <c>releases/latest</c>
/// redirect (one HEAD, no API allowance), so the list is there when the API is rate limited.
/// </summary>
public sealed partial class NvidiaRuntimes(GitHubReleases releases, ILogger<NvidiaRuntimes> logger)
{
    private const string Folder = "lib/Windows_x86_64/rel";

    private static readonly GitHubSource _source = GitHubSource.Parse(ComponentRepositories.Nvidia);

    // The families the SDK ships a runtime of; each family's file name and component id come from its table.
    private static readonly BuildFamily[] _families = [BuildFamily.Dlss, BuildFamily.Dlssd, BuildFamily.Dlssg];

    // An empty list is the redirect naming no release: not kept, like a failure.
    private readonly ResolutionMemory<bool, IReadOnlyList<BuildEntry>> _memory = new(entries => entries.Count > 0);

    /// <summary>
    /// One entry per runtime, labelled <c>&lt;sdk tag&gt; (NVIDIA SDK)</c>, remembered for the process;
    /// none when the redirect fails (a refusal included: a blocked host, a downgrade) or names no
    /// release, and that is not remembered: the next call asks again. The mirror alone then serves
    /// the families, and the catalog says so (<see cref="BuildCatalog.NvidiaListUnread"/>).
    /// </summary>
    public async Task<IReadOnlyList<BuildEntry>> GetAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _memory.GetAsync(true, ResolveAsync, cancellationToken);
        }
        catch (NetException failure)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                LogFailed(logger, failure.Host, failure.Kind);
            }

            return [];
        }
    }

    /// <exception cref="NetException">The redirect could not be asked.</exception>
    private async Task<IReadOnlyList<BuildEntry>> ResolveAsync(CancellationToken cancellationToken)
    {
        if (await releases.GetLatestTagAsync(_source.Repository, cancellationToken) is not { } tag)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                LogNoTag(logger);
            }

            return [];
        }

        // One SDK ships all three and NVIDIA numbers each runtime on its own, so the label names the SDK.
        string label = tag.TrimStart('v', 'V') + " (NVIDIA SDK)";
        return [.. _families.Select(family => Runtime(family, tag, label))];
    }

    // The family's DLL in the SDK's tree at the tag, checked by the signer of NVIDIA's own catalog row for it.
    private static BuildEntry Runtime(BuildFamily family, string tag, string label) =>
        new(
            family,
            BuildPublisher.Nvidia,
            tag,
            label,
            _source.Repository.RawFile(Uri.EscapeDataString(tag), $"{Folder}/{MirrorFamilies.NameOf(family)}"),
            MirrorFamilies.NameOf(family),
            ComponentCatalog.Of(MirrorFamilies.IdOf(family), _source).Signer ?? throw new InvalidOperationException($"{family} is not checked by its signer"));

    [LoggerMessage(Level = LogLevel.Warning, Message = "NVIDIA's latest-release redirect failed at {Host} ({Kind}); the mirror alone serves the DLSS runtimes this time")]
    private static partial void LogFailed(ILogger logger, string host, NetErrorKind kind);

    [LoggerMessage(Level = LogLevel.Warning, Message = "NVIDIA's latest-release redirect names no release; the mirror alone serves the DLSS runtimes this time")]
    private static partial void LogNoTag(ILogger logger);
}
