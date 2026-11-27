using System.Net;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.GitHub;
using Autopilot.Core.Net;
using Autopilot.Core.Resources;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Components.Sources;

/// <summary>
/// The ReShade add-on installer, resolved once per run: the link on reshade.me's home page; then
/// the newest version tag of <c>crosire/reshade</c>, which publishes tags and no installer, so the
/// address is built from the version; then the newest installer already in the download cache.
/// When all three fail, the error lists every reason.
/// </summary>
public sealed partial class ReShadeSource
{
    private const string Home = "https://reshade.me/";

    // The GitHub API lists tags by name, not by version (v6.10.0 sorts after v6.9.0), so one full
    // page is read and the newest taken; the answer is kept for six hours like any API answer.
    private static readonly Uri _tags = GitHubRepository.Parse(ComponentRepositories.ReShade).Tags(100);

    // reshade.me answers 500 to about every other request on some days, with the whole page as the
    // body: a 5xx that still holds the link is the answer. A bare 5xx is retried by the network
    // client's own attempts (three, with growing waits), so no loop of our own is needed here.
    private static readonly TextRequest _homeRequest = new() { AcceptServerError = ReShadeVersions.HasSetupLink };

    // An installer found only in the cache stands in while the network is out, and is asked again next time.
    private readonly ResolutionMemory<bool, ReShadeInstaller> _memory;

    private readonly NetClient _client;
    private readonly DownloadCache _cache;
    private readonly ILogger<ReShadeSource> _logger;

    /// <summary>Built by a <see cref="ComponentRun"/>; <paramref name="run"/> is its token, which the shared resolutions run under.</summary>
    internal ReShadeSource(NetClient client, DownloadCache cache, ILogger<ReShadeSource> logger, CancellationToken run)
    {
        _memory = new(run, installer => installer.Origin != ReShadeOrigin.Cache);
        _client = client;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>The installer to download; the same answer for the rest of the run once found.</summary>
    /// <exception cref="ComponentException">No source named an installer (<see cref="ComponentFailure.InstallerNotFound"/>); the message and <see cref="ComponentException.Reasons"/> list why.</exception>
    /// <exception cref="NetException">A source was refused (a redirect off the list, a downgrade): never covered by another.</exception>
    public Task<ReShadeInstaller> ResolveAsync(CancellationToken cancellationToken) =>
        _memory.GetAsync(true, ResolveOnceAsync, cancellationToken);

    private async Task<ReShadeInstaller> ResolveOnceAsync(CancellationToken cancellationToken)
    {
        List<string> reasons = [];
        ReShadeInstaller installer = await FromHomePageAsync(reasons, cancellationToken)
            ?? await FromTagsAsync(reasons, cancellationToken)
            ?? FromCache(reasons)
            ?? throw new ComponentException(ComponentFailure.InstallerNotFound, Texts.Format("ReShadeNotFound", string.Join("; ", reasons))) { Reasons = reasons };
        if (_logger.IsEnabled(LogLevel.Information))
        {
            LogResolved(_logger, installer.Version.Label, installer.Origin);
        }

        return installer;
    }

    private async Task<ReShadeInstaller?> FromHomePageAsync(List<string> reasons, CancellationToken cancellationToken)
    {
        if (await TryGetAsync(new(Home), _homeRequest, reasons, cancellationToken) is not { } answer)
        {
            return null;
        }

        if (ReShadeVersions.FromHomePage(answer.Body) is { } version)
        {
            return new(version, ReShadeOrigin.HomePage);
        }

        reasons.Add(Texts.Format("ReShadeNoLink", (int)answer.StatusCode));
        return null;
    }

    private async Task<ReShadeInstaller?> FromTagsAsync(List<string> reasons, CancellationToken cancellationToken)
    {
        if (await TryGetAsync(_tags, ReleaseResolver.ApiRequest, reasons, cancellationToken) is not { } answer)
        {
            return null;
        }

        ComponentVersion? newest = answer.StatusCode == HttpStatusCode.OK ? ReShadeVersions.NewestTag(answer.Body) : null;
        if (newest is null)
        {
            reasons.Add(Texts.Format("ReShadeNoTag", (int)answer.StatusCode));
            return null;
        }

        return new(newest, ReShadeOrigin.GitHubTag);
    }

    // The source's answer; null, with the failure added to the reasons, when the network failed. A refusal propagates.
    private Task<TextAnswer?> TryGetAsync(Uri url, TextRequest request, List<string> reasons, CancellationToken cancellationToken) =>
        NetFallback.OrNullAsync(_client.GetTextAsync(url, ComponentHosts.Allowlist, request, cancellationToken), failure => reasons.Add(failure.Message));

    private ReShadeInstaller? FromCache(List<string> reasons)
    {
        ComponentVersion? newest = ReShadeVersions.NewestCached(_cache.CachedNames(ReShadeInstaller.CachedPattern));
        if (newest is null)
        {
            reasons.Add(Texts.Text("ReShadeNotCached"));
            return null;
        }

        return new(newest, ReShadeOrigin.Cache);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "ReShade {Version}, from {Origin}")]
    private static partial void LogResolved(ILogger logger, string version, ReShadeOrigin origin);
}

/// <summary>Where the ReShade version came from.</summary>
public enum ReShadeOrigin
{
    /// <summary>The link on reshade.me's home page.</summary>
    HomePage,

    /// <summary>The newest version tag of <c>crosire/reshade</c>.</summary>
    GitHubTag,

    /// <summary>The newest installer already in the download cache.</summary>
    Cache,
}

/// <summary>The ReShade add-on installer of one version, and where it downloads from.</summary>
public sealed record ReShadeInstaller(ComponentVersion Version, ReShadeOrigin Origin)
{
    /// <summary>What an installer's name starts with, before its version.</summary>
    private const string NamePrefix = "ReShade_Setup_";

    /// <summary>What an installer's name ends with, after its version.</summary>
    private const string NameSuffix = "_Addon.exe";

    /// <summary>The cache names of installers, for the cache-only fallback.</summary>
    internal const string CachedPattern = NamePrefix + "*" + NameSuffix;

    /// <summary>Where reshade.me serves its installers.</summary>
    private const string Downloads = "https://reshade.me/downloads/";

    /// <summary>The installer's own name, which is also its readable name in the cache.</summary>
    public string FileName => NamePrefix + Version.Label + NameSuffix;

    /// <summary>reshade.me's address for this version; every source leads here.</summary>
    public Uri Url => new(Downloads + FileName);

    /// <summary>
    /// Only the <c>MZ</c> check runs: reshade.me publishes no sums, and whether the installer is
    /// signed, and by whom, is still to be verified.
    /// </summary>
    public Verification Verification { get; } = ComponentCatalog.Of(ComponentIds.ReShade).Unchecked();
}
