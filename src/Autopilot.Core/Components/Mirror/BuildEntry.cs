using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Net;

namespace Autopilot.Core.Components.Mirror;

/// <summary>One build of a family: where it downloads from and how the download and the file in it are checked.</summary>
/// <param name="Family">The family it belongs to.</param>
/// <param name="Publisher">The mirror (a <c>.zip</c> holding the file) or NVIDIA (the DLL itself).</param>
/// <param name="Tag">The release tag it comes from (<c>renodx-dlss5-4.55</c>, <c>v310.9.16</c>).</param>
/// <param name="Label">What the person picks and the install records (<c>4.55</c>, <c>310.8.0-RTX40</c>, <c>310.9.16 (NVIDIA SDK)</c>).</param>
/// <param name="Url">The download's address.</param>
/// <param name="FileName">The download's own name: the mirror's zip, or NVIDIA's DLL.</param>
/// <param name="Download">How the download proves it is the publisher's file before it enters the cache.</param>
public sealed record BuildEntry(BuildFamily Family, BuildPublisher Publisher, string Tag, string Label, Uri Url, string FileName, Verification Download)
{
    /// <summary>Whether the download is the DLL itself, which also passes the raw-DLL checks, rather than an archive holding it.</summary>
    public bool IsRaw => Publisher == BuildPublisher.Nvidia;

    /// <summary>
    /// The check the file taken out of the download must pass before it is placed, as the family's
    /// mirror row says: NVIDIA's signature on a file of NVIDIA's the mirror republishes unchanged
    /// (<c>nvngx_dlss.dll</c>, <c>nvngx_dlssd.dll</c>, <c>nvngx_dlssg.dll</c>); for an
    /// <c>nvngx_dlssnr.dll</c> build, the digest pinned for its label (<see cref="DlssnrPins"/>), else
    /// NVIDIA's signature; <see langword="null"/> when nothing beyond the download's own check applies
    /// (the RenoDX add-ons, and NVIDIA's own DLL, whose download is that check).
    /// </summary>
    public Verification? Contents =>
        Publisher != BuildPublisher.Mirror ? null : MirrorFamilies.EntryOf(Family).Contents switch
        {
            ContentsCheck.NvidiaSigner => CatalogEntry.NvidiaSignerCheck,
            ContentsCheck.PinnedDigestOrNvidiaSigner => DlssnrPins.Of(Label)?.Verification ?? (Verification)CatalogEntry.NvidiaSignerCheck,
            _ => null,
        };
}

/// <summary>The entry chosen from a family.</summary>
/// <param name="Entry">The chosen entry.</param>
/// <param name="WantedMissing">
/// A label or tag was asked for and the family has none: the newest was taken instead, and the caller says so.
/// </param>
public sealed record PickedBuild(BuildEntry Entry, bool WantedMissing);
