using System.IO.Enumeration;
using Autopilot.Core.Components.GitHub;
using Autopilot.Core.Net;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Components.Catalog;

/// <summary>The ids of the components, as the install record stores them and the catalog lists them.</summary>
public static class ComponentIds
{
    public const string ReShade = "reshade";
    public const string ShaderHeaders = "shader_headers";
    public const string Renodx = "renodx";
    public const string RenodxSf = "renodx_sf";
    public const string Dlssnr = "dlssnr";
    public const string Dlss = "dlss";
    public const string Dlssd = "dlssd";
    public const string Dlssg = "dlssg";
    public const string Feeder = "feeder";
    public const string Lumenite = "lumenite";
    public const string Vort = "vort";
    public const string Bridge = "bridge";
    public const string Upstream = "upstream";
    public const string Standalone = "standalone";
    public const string Uplift = "uplift";
    public const string OptiScaler = "optiscaler";
    public const string Dxvk = "dxvk";
    public const string Reframework = "reframework";
    public const string Mfg = "mfg";
    public const string AsiLoader = "asi_loader";
    public const string RemixRuntime = "remix_runtime";
}

/// <summary>How a component's files prove they are the publisher's, as one of net's verification kinds.</summary>
public enum ComponentCheck
{
    /// <summary>The SHA-256 GitHub's release data gives for the asset; only the magic bytes when it gives none.</summary>
    ReleaseDigest,

    /// <summary>
    /// The release's sums file, when the release carries one, else the release data's SHA-256, else
    /// only the magic bytes.
    /// </summary>
    SumsWhenPublished,

    /// <summary>The release's sums file, which the publisher puts in every release: without one readable, nothing is installed.</summary>
    SumsRequired,

    /// <summary>NVIDIA's Authenticode signature on the file placed.</summary>
    NvidiaSigner,

    /// <summary>Nothing beyond the magic bytes; <see cref="CatalogEntry.UncheckedReason"/> says why.</summary>
    None,
}

/// <summary>One component of the catalog.</summary>
/// <param name="Id">The id the install record stores.</param>
/// <param name="Publisher">Who publishes it.</param>
/// <param name="Source">Where it is taken from: a GitHub repository, or a web site.</param>
/// <param name="Check">How its files prove themselves.</param>
/// <param name="UncheckedReason">
/// Why a file goes in with only its magic bytes checked: always for <see cref="ComponentCheck.None"/>,
/// and for a release that gives no digest otherwise.
/// </param>
/// <param name="SumsPattern">
/// The sums file's asset name for the sums checks, case ignored: a name, or a pattern where <c>*</c>
/// stands for any run of characters.
/// </param>
public sealed record CatalogEntry(string Id, string Publisher, string Source, ComponentCheck Check, string UncheckedReason, string? SumsPattern = null)
{
    /// <summary>The signer NVIDIA's runtimes carry. The one place the name is written.</summary>
    internal const string NvidiaPublisher = "NVIDIA Corporation";

    /// <summary>The signer check, as one of net's verification kinds.</summary>
    internal static SignerVerification NvidiaSignerCheck { get; } = new(NvidiaPublisher);

    /// <summary>
    /// The signer this component's files must carry: NVIDIA's for <see cref="ComponentCheck.NvidiaSigner"/>;
    /// <see langword="null"/> for every other check.
    /// </summary>
    internal SignerVerification? Signer => Check == ComponentCheck.NvidiaSigner ? NvidiaSignerCheck : null;

    /// <summary>The source as a GitHub repository.</summary>
    /// <exception cref="ArgumentException">The source is not a GitHub repository (a web site).</exception>
    internal GitHubRepository Repository => GitHubRepository.Parse(Source);

    /// <summary>
    /// The check for <paramref name="asset"/> of <paramref name="release"/>, as this entry says: the sums
    /// file the release carries, the release data's digest, or only the magic bytes.
    /// </summary>
    /// <param name="release">The release the asset is taken from.</param>
    /// <param name="asset">The asset to check.</param>
    /// <param name="sumsMissing">The words shown when sums are required and missing; unset, the catalog's own.</param>
    /// <exception cref="ComponentException">The publisher puts sums in every release and this one carries none.</exception>
    /// <exception cref="ArgumentException">This component is not checked by its release data.</exception>
    internal Verification VerificationOf(GitHubRelease release, GitHubAsset asset, string? sumsMissing = null)
    {
        SumsFileVerification? sums = SumsPattern is { } pattern
            && release.Assets.FirstOrDefault(candidate => FileSystemName.MatchesSimpleExpression(pattern, candidate.Name, ignoreCase: true)) is { } listed
            ? new(listed.Url, asset.Name)
            : null;
        return Check switch
        {
            ComponentCheck.SumsRequired => sums
                ?? throw new ComponentException(
                    ComponentFailure.SumsMissing,
                    sumsMissing ?? Texts.Format("ComponentSumsMissing", Source, release.Tag, SumsPattern)),
            ComponentCheck.SumsWhenPublished => sums ?? DigestOr(asset),
            ComponentCheck.ReleaseDigest => DigestOr(asset),
            _ => throw new ArgumentException($"'{Id}' is not checked by its release data"),
        };
    }

    /// <summary>The SHA-256 GitHub's release data gives for <paramref name="asset"/>; only the magic bytes, and this entry's reason, when it gives none.</summary>
    internal Verification DigestOr(GitHubAsset asset) => asset.Digest ?? (Verification)Unchecked();

    /// <summary>Only the magic bytes, and why: this entry's own reason, or <paramref name="why"/> (a file taken with no release data).</summary>
    internal NoVerification Unchecked(string? why = null) => new($"{Source}: {why ?? UncheckedReason}");

    /// <summary>The release tagged <paramref name="tag"/> lacks <paramref name="missing"/>: it changed its layout, and a newer layout is not taken on trust.</summary>
    internal ComponentException LayoutChanged(string tag, string missing) =>
        new(ComponentFailure.LayoutChanged, Texts.Format("ComponentLayoutChanged", Source, tag, missing));

    /// <summary>None of the releases read carries <paramref name="asset"/>; <paramref name="newestTag"/> is the newest of them, <see langword="null"/> when there were none.</summary>
    internal ComponentException NoneCarries(string asset, string? newestTag) =>
        new(ComponentFailure.LayoutChanged, Texts.Format("ComponentNoneCarries", Source, asset, newestTag ?? "none"));
}

/// <summary>
/// Every component the routes install, as data: its publisher, its source and how it is verified;
/// the area's requests and redirects stay on <see cref="ComponentHosts.Allowlist"/>. A component cannot
/// move to another host or lose its check without a change to this table or that list.
/// </summary>
public static class ComponentCatalog
{
    private const string NoDigest = "the release data gave no digest";
    private const string Sums = "SHA256SUMS.txt";
    private const string CommitArchive = "a commit's archive has no published digest; the commit is the version recorded";

    /// <summary>Every component, in the order an install names them.</summary>
    public static IReadOnlyList<CatalogEntry> All { get; } =
    [
        new(ComponentIds.ReShade, "crosire", "reshade.me", ComponentCheck.None, "no publisher sums or signature verified"),
        new(ComponentIds.ShaderHeaders, "crosire", ComponentRepositories.ShaderHeaders, ComponentCheck.None, CommitArchive),
        new(ComponentIds.Renodx, "Krish, through the mirror", ComponentRepositories.Mirror, ComponentCheck.ReleaseDigest, NoDigest),
        new(ComponentIds.RenodxSf, "ShortFuse, through the mirror", ComponentRepositories.Mirror, ComponentCheck.ReleaseDigest, NoDigest),
        new(ComponentIds.Dlssnr, "NVIDIA, through the mirror", ComponentRepositories.Mirror, ComponentCheck.NvidiaSigner, NoDigest),
        new(ComponentIds.Dlss, "NVIDIA", ComponentRepositories.Nvidia, ComponentCheck.NvidiaSigner, NoDigest),
        new(ComponentIds.Dlssd, "NVIDIA", ComponentRepositories.Nvidia, ComponentCheck.NvidiaSigner, NoDigest),
        new(ComponentIds.Dlssg, "NVIDIA", ComponentRepositories.Nvidia, ComponentCheck.NvidiaSigner, NoDigest),
        new(ComponentIds.Feeder, "jlrouzies-fr", ComponentRepositories.Feeder, ComponentCheck.ReleaseDigest, NoDigest),
        new(ComponentIds.Lumenite, "umar-afzaal", ComponentRepositories.Lumenite, ComponentCheck.None, CommitArchive),
        new(ComponentIds.Vort, "vortigern11", ComponentRepositories.Vort, ComponentCheck.None, CommitArchive),
        new(ComponentIds.Bridge, "NIGos", ComponentRepositories.Bridge, ComponentCheck.ReleaseDigest, NoDigest),
        new(ComponentIds.Upstream, "matiasLombo", ComponentRepositories.Upstream, ComponentCheck.ReleaseDigest, NoDigest),
        new(ComponentIds.Standalone, "kibblerz", ComponentRepositories.Standalone, ComponentCheck.ReleaseDigest, NoDigest),
        new(ComponentIds.Uplift, "ghostinthecamera", ComponentRepositories.Uplift, ComponentCheck.SumsRequired, NoDigest, Sums),
        // A build that publishes sums in every release requires them (OptiScalerBuild.PublishesSums);
        // the sums file's name varies by build.
        new(ComponentIds.OptiScaler, "the build's publisher", ComponentRepositories.OptiScaler, ComponentCheck.SumsWhenPublished, NoDigest, "*SHA256SUMS*"),
        new(ComponentIds.Dxvk, "doitsujin", ComponentRepositories.Dxvk, ComponentCheck.ReleaseDigest, NoDigest),
        new(ComponentIds.Reframework, "praydog", ComponentRepositories.Reframework, ComponentCheck.ReleaseDigest, NoDigest),
        new(ComponentIds.Mfg, "dashdogy", ComponentRepositories.Mfg, ComponentCheck.ReleaseDigest, NoDigest),
        new(ComponentIds.AsiLoader, "ThirteenAG", ComponentRepositories.AsiLoader, ComponentCheck.ReleaseDigest, NoDigest),
        new(ComponentIds.RemixRuntime, "lunks", ComponentRepositories.RemixRuntime, ComponentCheck.SumsWhenPublished, NoDigest, Sums),
    ];

    /// <summary>The component whose id is <paramref name="id"/>.</summary>
    /// <exception cref="ArgumentException">No component has that id.</exception>
    public static CatalogEntry Of(string id) =>
        All.FirstOrDefault(entry => entry.Id == id) ?? throw new ArgumentException($"'{id}' is not a component", nameof(id));
}

/// <summary>The GitHub repositories of the components this area resolves.</summary>
internal static class ComponentRepositories
{
    /// <summary>The mirror that carries the <c>nvngx_dlssnr.dll</c> and RenoDX builds.</summary>
    public const string Mirror = "RankFTW/rhi-repo";

    /// <summary>NVIDIA's SDK, whose tree holds NVIDIA's own runtimes.</summary>
    public const string Nvidia = "NVIDIA/DLSS";

    public const string Feeder = "jlrouzies-fr/DLSS5-Feeder";

    /// <summary>The default OptiScaler build's repository (<c>OptiScalerBuild.Default</c>).</summary>
    public const string OptiScaler = "Dagherbou/OptiScaler_DLSSNR";

    /// <summary>ReShade's source, which publishes version tags and no installer.</summary>
    public const string ReShade = "crosire/reshade";

    public const string Lumenite = "umar-afzaal/LumeniteFX";
    public const string Vort = "vortigern11/vort_Shaders";
    public const string ShaderHeaders = "crosire/reshade-shaders";
    public const string Bridge = "NIGos/dlss5-bridge";
    public const string Upstream = "matiasLombo/neural-upstream";
    public const string Standalone = "kibblerz/DLSS5-Reshade-AIO";
    public const string Uplift = "ghostinthecamera/GITC-Uplift";
    public const string Dxvk = "doitsujin/dxvk";
    public const string Reframework = "praydog/REFramework-nightly";
    public const string Mfg = "dashdogy/MFG-Unlocker";
    public const string AsiLoader = "ThirteenAG/Ultimate-ASI-Loader";
    public const string RemixRuntime = "lunks/dxvk-remix-plus-dlssnr";
}
