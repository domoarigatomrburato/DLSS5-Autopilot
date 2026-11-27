using Autopilot.Core.Net;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Components;

/// <summary>
/// NVIDIA's own <c>nvngx_dlss.dll</c>, <c>nvngx_dlssd.dll</c> and <c>nvngx_dlssg.dll</c>, read from the
/// tree of the <c>NVIDIA/DLSS</c> SDK's newest tag. The tag comes from the <c>releases/latest</c>
/// redirect (one HEAD, no API allowance), so the list is there when the API is rate limited.
/// </summary>
public sealed partial class NvidiaRuntimes(NetClient client, ILogger<NvidiaRuntimes> logger)
{
    /// <summary>NVIDIA's SDK repository.</summary>
    public const string Repository = "NVIDIA/DLSS";

    private const string Folder = "lib/Windows_x86_64/rel";

    private static readonly GitHubRepository _repository = GitHubRepository.Parse(Repository);

    private static readonly (BuildFamily Family, string Name)[] _files =
    [
        (BuildFamily.Dlss, "nvngx_dlss.dll"),
        (BuildFamily.Dlssd, "nvngx_dlssd.dll"),
        (BuildFamily.Dlssg, "nvngx_dlssg.dll"),
    ];

    private IReadOnlyList<BuildEntry>? _remembered;

    /// <summary>
    /// One entry per runtime, labelled <c>&lt;sdk tag&gt; (NVIDIA SDK)</c>, remembered for the process;
    /// none when the redirect fails, and that failure is not remembered: the next call asks again.
    /// </summary>
    public async Task<IReadOnlyList<BuildEntry>> GetAsync(CancellationToken cancellationToken)
    {
        if (_remembered is { } remembered)
        {
            return remembered;
        }

        if (await TagAsync(cancellationToken) is not { } tag)
        {
            return [];
        }

        // One SDK ships all three and NVIDIA numbers each runtime on its own, so the label names the SDK.
        string label = tag.TrimStart('v', 'V') + " (NVIDIA SDK)";
        IReadOnlyList<BuildEntry> entries =
        [
            .. _files.Select(file => new BuildEntry(
                file.Family,
                BuildPublisher.Nvidia,
                tag,
                label,
                new Uri($"https://raw.githubusercontent.com/{_repository.Owner}/{_repository.Name}/{Uri.EscapeDataString(tag)}/{Folder}/{file.Name}"),
                file.Name,
                NvidiaFiles.Signer)),
        ];
        _remembered = entries;
        return entries;
    }

    private async Task<string?> TagAsync(CancellationToken cancellationToken)
    {
        HeadAnswer head;
        try
        {
            head = await client.HeadAsync(_repository.Latest, ComponentHosts.Allowlist, cancellationToken);
        }
        catch (NetException failure)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                LogFailed(logger, failure.Host, failure.Kind);
            }

            return null;
        }

        string? tag = head.Location is { } location ? _repository.TagIn(location) : null;
        if (tag is null && logger.IsEnabled(LogLevel.Warning))
        {
            LogNoTag(logger, (int)head.StatusCode, head.Location?.AbsoluteUri);
        }

        return tag;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "NVIDIA's latest-release redirect failed at {Host} ({Kind}); the mirror alone serves the DLSS runtimes this time")]
    private static partial void LogFailed(ILogger logger, string host, NetErrorKind kind);

    [LoggerMessage(Level = LogLevel.Warning, Message = "NVIDIA's latest-release redirect answered HTTP {Status} pointing at {Location}, which names no release; the mirror alone serves the DLSS runtimes this time")]
    private static partial void LogNoTag(ILogger logger, int status, string? location);
}
