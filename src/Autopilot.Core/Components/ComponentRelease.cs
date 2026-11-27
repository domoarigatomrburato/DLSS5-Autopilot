using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.GitHub;
using Autopilot.Core.Components.Mirror;
using Autopilot.Core.Net;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Components;

/// <summary>
/// One component as resolved for an install: the version to record, what to download and how each
/// download proves itself, and the files the downloads must hold before anything is placed.
/// </summary>
/// <param name="Id">The catalog id (<see cref="ComponentIds"/>).</param>
/// <param name="Version">The tag or commit recorded; <see cref="ComponentVersion.Latest"/> when only GitHub's redirect knew it.</param>
/// <param name="Downloads">What is fetched: loose files and archives.</param>
/// <param name="Files">The files the downloads must hold, loose or inside an archive.</param>
/// <param name="Source">Where the release data came from; <see langword="null"/> when none was read.</param>
public sealed record ComponentRelease(
    string Id,
    ComponentVersion Version,
    IReadOnlyList<ComponentDownload> Downloads,
    IReadOnlyList<ExpectedFile> Files,
    ReleaseSource? Source)
{
    /// <summary>
    /// The words the install shows about where this came from: the source's notice, or the
    /// <c>latest/download</c> notice for a version nobody could read; <see langword="null"/> when there is nothing to say.
    /// </summary>
    public string? Notice => Source?.Notice ?? (Version.IsComparable ? null : Texts.Text("ReleasesFromLatestRedirect"));
}

/// <summary>One download of a component.</summary>
/// <param name="FileName">The publisher's own name for it, which is also its readable name in the cache.</param>
/// <param name="Url">Where it downloads from.</param>
/// <param name="Verification">How it proves itself before it enters the cache.</param>
public sealed record ComponentDownload(string FileName, Uri Url, Verification Verification)
{
    private static readonly string[] _archiveEndings = [".zip", ".7z", ".tar.gz", ".tgz"];

    /// <summary>Whether the download is an archive, unpacked in the cache, rather than a file placed as it is.</summary>
    public bool IsArchive => Array.Exists(_archiveEndings, ending => FileName.EndsWith(ending, StringComparison.OrdinalIgnoreCase));
}

/// <summary>A file a component's downloads must hold.</summary>
/// <param name="Path">
/// Its name, or the end of its path inside the archive (<c>x64/d3d11.dll</c>, <c>Shaders/vort_Motion.fx</c>),
/// with <c>/</c> between folders and case ignored.
/// </param>
/// <param name="Optional">A file some releases carry and others do not (<c>StandaloneBoundary.fx</c>).</param>
public sealed record ExpectedFile(string Path, bool Optional = false);

/// <summary>Why a component could not be resolved or fetched.</summary>
public enum ComponentFailure
{
    /// <summary>The repository has no release, or none of the releases read carries the component.</summary>
    NoRelease,

    /// <summary>The release is not of the shape the tool knows how to place: a newer layout is not taken on trust.</summary>
    LayoutChanged,

    /// <summary>A publisher that puts sums in every release has none in this one.</summary>
    SumsMissing,

    /// <summary>An add-on that is not a Windows binary of the bitness its name says.</summary>
    WrongBitness,

    /// <summary>A branch could not be resolved to a commit: nothing is downloaded from a moving branch.</summary>
    BranchUnavailable,

    /// <summary>No recent release on an OptiScaler build's page carries its package.</summary>
    NoPackage,

    /// <summary>An OptiScaler package holds no root-level <c>OptiScaler.dll</c>: it is some other program.</summary>
    NotOptiScaler,

    /// <summary>The feeder's exact tag is not among its 15 newest releases.</summary>
    TagNotListed,

    /// <summary>A feeder release carries neither the feeder's zip nor its loose files.</summary>
    NoFiles,

    /// <summary>No source named a ReShade installer: reshade.me, GitHub's tags and the download cache all failed.</summary>
    InstallerNotFound,

    /// <summary>A pinned <c>renodx-dlss5</c> build is not on the build list: the newest is the kind of build the pin exists to avoid.</summary>
    PinMissing,

    /// <summary>A family's build list came back empty: GitHub could not be reached and nothing was kept.</summary>
    BuildListUnavailable,
}

/// <summary>A component refused before anything was written to a game, with the words the install shows.</summary>
public sealed class ComponentException(ComponentFailure reason, string message, Exception? innerException = null) : Exception(message, innerException)
{
    public ComponentFailure Reason { get; } = reason;

    /// <summary>For <see cref="ComponentFailure.InstallerNotFound"/>: why each source failed, in the order they were asked; empty otherwise.</summary>
    public IReadOnlyList<string> Reasons { get; init; } = [];

    /// <summary>For <see cref="ComponentFailure.PinMissing"/>: the pinned label that is missing.</summary>
    public string? Label { get; init; }

    /// <summary>For <see cref="ComponentFailure.BuildListUnavailable"/>: the family that has no builds.</summary>
    public BuildFamily? Family { get; init; }

    /// <summary>The family's build list could not be read.</summary>
    internal static ComponentException BuildListUnavailable(BuildFamily family) =>
        new(ComponentFailure.BuildListUnavailable, Texts.Format("BuildListEmpty", MirrorFamilies.NameOf(family))) { Family = family };
}
