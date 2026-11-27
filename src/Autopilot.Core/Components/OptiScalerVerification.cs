using Autopilot.Core.Net;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Components;

/// <summary>How an OptiScaler package proves itself before it enters the cache.</summary>
internal static class OptiScalerVerification
{
    /// <summary>
    /// The check the release offers: the API's digest, else its <c>SHA256SUMS</c> file, else only the
    /// magic bytes. jlrouzies' fork publishes sums in every release: its sums are always used, and
    /// without them nothing is installed unchecked.
    /// </summary>
    /// <exception cref="OptiScalerException">The build publishes sums and this release carries none.</exception>
    public static Verification Of(OptiScalerBuild build, GitHubRelease release, GitHubAsset archive)
    {
        SumsFileVerification? sums = release.Assets
            .Where(asset => asset.Name.Contains("SHA256SUMS", StringComparison.OrdinalIgnoreCase))
            .Select(asset => new SumsFileVerification(asset.Url, archive.Name))
            .FirstOrDefault();
        if (build.PublishesSums)
        {
            return sums ?? throw new OptiScalerException(OptiScalerFailure.SumsMissing, Texts.Format("OptiScalerSumsMissing", release.Tag, build.Repository));
        }

        return archive.Digest ?? sums ?? (Verification)new NoVerification("the release gives no digest and no sums file");
    }
}
