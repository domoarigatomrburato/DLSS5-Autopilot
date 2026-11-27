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

/// <summary>
/// The check the file taken out of a download must pass before it is placed, beside the download's
/// own <see cref="ComponentCheck"/>: a mirror zip is checked by its release digest, the DLL inside
/// it by this.
/// </summary>
public enum ContentsCheck
{
    /// <summary>Nothing beyond the download's own check: the download is the file, or the file inside it is taken on the download's word (the RenoDX add-ons).</summary>
    None,

    /// <summary>NVIDIA's Authenticode signature: a file of NVIDIA's the mirror republishes unchanged.</summary>
    NvidiaSigner,

    /// <summary>
    /// The SHA-256 pinned for the build's label in the release's data (a community
    /// <c>nvngx_dlssnr.dll</c> build, which no one signs), else NVIDIA's signature (NVIDIA's own build).
    /// </summary>
    PinnedDigestOrNvidiaSigner,
}

/// <summary>Where a component is taken from. <see cref="object.ToString"/> names it for messages.</summary>
public abstract record ComponentSource
{
    private protected ComponentSource()
    {
    }
}

/// <summary>A GitHub repository: its releases, branches or raw files.</summary>
/// <param name="Repository">The repository.</param>
internal sealed record GitHubSource(GitHubRepository Repository) : ComponentSource
{
    /// <summary>The source for <c>owner/name</c>.</summary>
    /// <exception cref="ArgumentException"><paramref name="repository"/> is not <c>owner/name</c>.</exception>
    public static GitHubSource Parse(string repository) => new(GitHubRepository.Parse(repository));

    /// <summary><c>owner/name</c>.</summary>
    public override string ToString() => Repository.ToString();
}

/// <summary>A web site that is not GitHub (ReShade's <c>reshade.me</c>).</summary>
/// <param name="Site">The site's host name.</param>
internal sealed record WebSource(string Site) : ComponentSource
{
    /// <summary>The site's host name.</summary>
    public override string ToString() => Site;
}

/// <summary>One row of the catalog: one component from one source.</summary>
/// <param name="Id">The id the install record stores; several rows share it when the component has several sources.</param>
/// <param name="Publisher">Who publishes it.</param>
/// <param name="Source">Where it is taken from: a GitHub repository, or a web site.</param>
/// <param name="Check">How the download proves itself.</param>
/// <param name="UncheckedReason">
/// Why a file goes in with only its magic bytes checked: always for <see cref="ComponentCheck.None"/>,
/// and for a release that gives no digest otherwise.
/// </param>
/// <param name="SumsPattern">
/// The sums file's asset name for the sums checks, case ignored: a name, or a pattern where <c>*</c>
/// stands for any run of characters.
/// </param>
/// <param name="Contents">How the file taken out of the download proves itself, beside <paramref name="Check"/>.</param>
public sealed record CatalogEntry(
    string Id,
    string Publisher,
    ComponentSource Source,
    ComponentCheck Check,
    string UncheckedReason,
    string? SumsPattern = null,
    ContentsCheck Contents = ContentsCheck.None)
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

    /// <summary>The source as a GitHub repository, for the components taken from GitHub.</summary>
    /// <exception cref="InvalidOperationException">The source is not on GitHub (a web site).</exception>
    internal GitHubRepository Repository =>
        Source is GitHubSource github ? github.Repository : throw new InvalidOperationException($"'{Id}' is not taken from GitHub but from {Source}");

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
/// Every component the routes install, as data, one row per source: its publisher, its source and
/// how its files are verified; the area's requests and redirects stay on <see cref="ComponentHosts.Allowlist"/>.
/// A component cannot move to another host or lose its check without a change to this table or that
/// list, and no row is borrowed by another source and rewritten in code.
/// </summary>
public static class ComponentCatalog
{
    private const string NoDigest = "the release data gave no digest";
    private const string Sums = "SHA256SUMS.txt";
    private const string OptiScalerSums = "*SHA256SUMS*";
    private const string CommitArchive = "a commit's archive has no published digest; the commit is the version recorded";
    private const string ThroughTheMirror = "NVIDIA, through the mirror";

    /// <summary>Every row, in the order an install names the components.</summary>
    public static IReadOnlyList<CatalogEntry> All { get; } =
    [
        new(ComponentIds.ReShade, "crosire", new WebSource("reshade.me"), ComponentCheck.None, "no publisher sums or signature verified"),
        new(ComponentIds.ShaderHeaders, "crosire", GitHub(ComponentRepositories.ShaderHeaders), ComponentCheck.None, CommitArchive),
        new(ComponentIds.Renodx, "Krish, through the mirror", GitHub(ComponentRepositories.Mirror), ComponentCheck.ReleaseDigest, NoDigest),
        new(ComponentIds.RenodxSf, "ShortFuse, through the mirror", GitHub(ComponentRepositories.Mirror), ComponentCheck.ReleaseDigest, NoDigest),

        // The mirror's zips are checked by their release digest, the DLL inside by NVIDIA's signer or a pinned digest.
        new(ComponentIds.Dlssnr, ThroughTheMirror, GitHub(ComponentRepositories.Mirror), ComponentCheck.ReleaseDigest, NoDigest, Contents: ContentsCheck.PinnedDigestOrNvidiaSigner),

        // NVIDIA's own rows download the DLL itself, so the download's check is the signer.
        new(ComponentIds.Dlss, "NVIDIA", GitHub(ComponentRepositories.Nvidia), ComponentCheck.NvidiaSigner, NoDigest),
        new(ComponentIds.Dlss, ThroughTheMirror, GitHub(ComponentRepositories.Mirror), ComponentCheck.ReleaseDigest, NoDigest, Contents: ContentsCheck.NvidiaSigner),
        new(ComponentIds.Dlssd, "NVIDIA", GitHub(ComponentRepositories.Nvidia), ComponentCheck.NvidiaSigner, NoDigest),
        new(ComponentIds.Dlssd, ThroughTheMirror, GitHub(ComponentRepositories.Mirror), ComponentCheck.ReleaseDigest, NoDigest, Contents: ContentsCheck.NvidiaSigner),
        new(ComponentIds.Dlssg, "NVIDIA", GitHub(ComponentRepositories.Nvidia), ComponentCheck.NvidiaSigner, NoDigest),
        new(ComponentIds.Dlssg, ThroughTheMirror, GitHub(ComponentRepositories.Mirror), ComponentCheck.ReleaseDigest, NoDigest, Contents: ContentsCheck.NvidiaSigner),
        new(ComponentIds.Feeder, "jlrouzies-fr", GitHub(ComponentRepositories.Feeder), ComponentCheck.ReleaseDigest, NoDigest),
        new(ComponentIds.Lumenite, "umar-afzaal", GitHub(ComponentRepositories.Lumenite), ComponentCheck.None, CommitArchive),
        new(ComponentIds.Vort, "vortigern11", GitHub(ComponentRepositories.Vort), ComponentCheck.None, CommitArchive),
        new(ComponentIds.Bridge, "NIGos", GitHub(ComponentRepositories.Bridge), ComponentCheck.ReleaseDigest, NoDigest),
        new(ComponentIds.Upstream, "matiasLombo", GitHub(ComponentRepositories.Upstream), ComponentCheck.ReleaseDigest, NoDigest),
        new(ComponentIds.Standalone, "kibblerz", GitHub(ComponentRepositories.Standalone), ComponentCheck.ReleaseDigest, NoDigest),
        new(ComponentIds.Uplift, "ghostinthecamera", GitHub(ComponentRepositories.Uplift), ComponentCheck.SumsRequired, NoDigest, Sums),

        // One row per OptiScaler page (wilsjo2's two builds share theirs); jlrouzies puts a sums file in every release.
        new(ComponentIds.OptiScaler, "Dagherbou", GitHub(ComponentRepositories.OptiScaler), ComponentCheck.SumsWhenPublished, NoDigest, OptiScalerSums),
        new(ComponentIds.OptiScaler, "y4my4my4m", GitHub(ComponentRepositories.OptiScalerY4my4my4m), ComponentCheck.SumsWhenPublished, NoDigest, OptiScalerSums),
        new(ComponentIds.OptiScaler, "wilsjo2", GitHub(ComponentRepositories.OptiScalerWilsjo2), ComponentCheck.SumsWhenPublished, NoDigest, OptiScalerSums),
        new(ComponentIds.OptiScaler, "Janblade", GitHub(ComponentRepositories.OptiScalerJanblade), ComponentCheck.SumsWhenPublished, NoDigest, OptiScalerSums),
        new(ComponentIds.OptiScaler, "jlrouzies-fr", GitHub(ComponentRepositories.OptiScalerJlrouzies), ComponentCheck.SumsRequired, NoDigest, OptiScalerSums),
        new(ComponentIds.Dxvk, "doitsujin", GitHub(ComponentRepositories.Dxvk), ComponentCheck.ReleaseDigest, NoDigest),
        new(ComponentIds.Reframework, "praydog", GitHub(ComponentRepositories.Reframework), ComponentCheck.ReleaseDigest, NoDigest),
        new(ComponentIds.Mfg, "dashdogy", GitHub(ComponentRepositories.Mfg), ComponentCheck.ReleaseDigest, NoDigest),
        new(ComponentIds.AsiLoader, "ThirteenAG", GitHub(ComponentRepositories.AsiLoader), ComponentCheck.ReleaseDigest, NoDigest),
        new(ComponentIds.RemixRuntime, "lunks", GitHub(ComponentRepositories.RemixRuntime), ComponentCheck.SumsWhenPublished, NoDigest, Sums),
    ];

    /// <summary>Every component's id once, in the order an install names them.</summary>
    public static IReadOnlyList<string> Ids { get; } = [.. All.Select(entry => entry.Id).Distinct(StringComparer.Ordinal)];

    /// <summary>The row of the component whose id is <paramref name="id"/>, a component with one source.</summary>
    /// <exception cref="ArgumentException">No row has that id, or several do: the caller must say which source.</exception>
    public static CatalogEntry Of(string id)
    {
        CatalogEntry[] rows = [.. All.Where(entry => entry.Id == id)];
        return rows.Length switch
        {
            1 => rows[0],
            0 => throw new ArgumentException($"'{id}' is not a component", nameof(id)),
            _ => throw new ArgumentException($"'{id}' has {rows.Length} sources; name the source", nameof(id)),
        };
    }

    /// <summary>The row of the component <paramref name="id"/> taken from <paramref name="source"/>.</summary>
    /// <exception cref="ArgumentException">No row has that id and source.</exception>
    public static CatalogEntry Of(string id, ComponentSource source) =>
        All.FirstOrDefault(entry => entry.Id == id && entry.Source == source)
            ?? throw new ArgumentException($"'{id}' is not taken from {source}", nameof(source));

    private static GitHubSource GitHub(string repository) => GitHubSource.Parse(repository);
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

    public const string OptiScalerY4my4my4m = "y4my4my4m/OptiScaler_DLSSNR_Multipass_MFG";

    /// <summary>wilsjo2's page, which carries both the standard build and the RTX 40 MFG package.</summary>
    public const string OptiScalerWilsjo2 = "wilsjo2/OptiScaler-DLSSNR-PreSR-Multipass";

    public const string OptiScalerJanblade = "Janblade/OptiScaler-F5-DLSSNR-Multipass";
    public const string OptiScalerJlrouzies = "jlrouzies-fr/OptiScaler-DLSSNR-PreSR-Multipass";

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
