using Autopilot.Core.Gpu;
using Autopilot.Core.Net;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Components;

/// <summary>
/// One of the six selectable OptiScaler builds: a release page, the words that name the variants
/// it passes over, and for a build that exists for one variant, that variant's word.
/// </summary>
/// <param name="Key">The key the CLI and settings name it by; empty for the default build.</param>
/// <param name="Repository">The GitHub repository whose releases carry it.</param>
/// <param name="SkipWords">Archive names carrying one of these are other variants, passed over.</param>
/// <param name="VariantWord">For a build that exists for one variant: only an archive carrying this word is taken.</param>
/// <param name="PublishesSums">The publisher puts a <c>SHA256SUMS</c> file in every release: without one readable, nothing is installed.</param>
/// <param name="OnlyCardSm">The one card architecture the build is for; <see langword="null"/> for any card.</param>
public sealed record OptiScalerBuild(string Key, string Repository, IReadOnlyList<string> SkipWords, string? VariantWord, bool PublishesSums, int? OnlyCardSm = null)
{
    /// <summary>Dagherbou's DLSS-NR build: <c>/latest</c>, then the release list.</summary>
    public static OptiScalerBuild Default { get; } = new("", "Dagherbou/OptiScaler_DLSSNR", [], null, PublishesSums: false);

    /// <summary>y4my4my4m's development builds in <c>.7z</c>; the <c>with_dlss</c> archives are passed over.</summary>
    public static OptiScalerBuild Y4my4my4m { get; } = new("y4my4my4m", "y4my4my4m/OptiScaler_DLSSNR_Multipass_MFG", ["with_dlss"], null, PublishesSums: false);

    /// <summary>wilsjo2's pass before super resolution; the RTX 40 MFG package is passed over.</summary>
    public static OptiScalerBuild Wilsjo2 { get; } = new("wilsjo2", "wilsjo2/OptiScaler-DLSSNR-PreSR-Multipass", ["rtx40-mfg"], null, PublishesSums: false);

    /// <summary>wilsjo2's RTX 40 MFG package only; refused on any card but RTX 40.</summary>
    public static OptiScalerBuild Wilsjo2Mfg { get; } = new("wilsjo2-mfg", "wilsjo2/OptiScaler-DLSSNR-PreSR-Multipass", [], "rtx40-mfg", PublishesSums: false, OnlyCardSm: 89);

    /// <summary>Janblade's fork of wilsjo2's; the <c>-test-</c> archives are passed over.</summary>
    public static OptiScalerBuild Janblade { get; } = new("janblade", "Janblade/OptiScaler-F5-DLSSNR-Multipass", ["-test-"], null, PublishesSums: false);

    /// <summary>jlrouzies' fork of wilsjo2's, settings re-read while the game runs; a <c>SHA256SUMS</c> file in every release.</summary>
    public static OptiScalerBuild Jlrouzies { get; } = new("jlrouzies", "jlrouzies-fr/OptiScaler-DLSSNR-PreSR-Multipass", ["rtx40-mfg"], null, PublishesSums: true);

    /// <summary>The six builds, the default first.</summary>
    public static IReadOnlyList<OptiScalerBuild> All { get; } = [Default, Y4my4my4m, Wilsjo2, Wilsjo2Mfg, Janblade, Jlrouzies];

    /// <summary>The build named <paramref name="key"/> (case ignored); <see langword="null"/> when there is none.</summary>
    public static OptiScalerBuild? ByKey(string key) =>
        All.FirstOrDefault(build => string.Equals(build.Key, key.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Why this build must not go onto <paramref name="card"/>, or <see langword="null"/>. Only the
    /// RTX 40 MFG package (sm 89, Ada) has a card; a card that could not be read is not refused,
    /// since nothing says it is the wrong one. The install asks before its first write.
    /// </summary>
    public string? CardRefusal(GpuCard? card) =>
        OnlyCardSm is { } only && card?.Sm is { } sm && sm != only ? Texts.Text("OptiScalerMfgCard") : null;

    /// <summary>
    /// How <paramref name="archive"/> of <paramref name="release"/> proves itself: the catalog's OptiScaler
    /// check for this build's own repository, which is the release's <c>SHA256SUMS</c> file, else the
    /// API's digest, else only the magic bytes. A build that publishes sums in every release always
    /// uses them, and without them nothing is installed unchecked.
    /// </summary>
    /// <exception cref="ComponentException">The build publishes sums and this release carries none.</exception>
    internal Verification VerificationOf(GitHubRelease release, GitHubAsset archive)
    {
        CatalogEntry entry = ComponentCatalog.Of(ComponentIds.OptiScaler) with
        {
            Source = Repository,
            Check = PublishesSums ? ComponentCheck.SumsRequired : ComponentCheck.SumsWhenPublished,
        };
        return entry.VerificationOf(release, archive, Texts.Format("OptiScalerSumsMissing", release.Tag, Repository));
    }
}
