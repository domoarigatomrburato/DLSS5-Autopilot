using Autopilot.Core.Net;

namespace Autopilot.Core.Components;

/// <summary>
/// Fetches a resolved component into the download cache and checks it before anything is placed:
/// every download verified as the catalog says, every archive unpacked in the cache, every expected
/// file present (a release of another shape is refused, never installed on trust), and every
/// <c>.addon64</c> or <c>.addon32</c> a Windows binary of that bitness.
/// </summary>
public sealed class ComponentFiles(DownloadCache cache)
{
    /// <summary>The component's files in the cache, checked.</summary>
    /// <exception cref="ComponentException">An expected file is missing, or an add-on is not of its bitness.</exception>
    /// <exception cref="NetException">A download failed, its content is wrong, or its verification failed.</exception>
    /// <exception cref="Archives.ArchiveException">An archive was refused.</exception>
    /// <exception cref="Storage.DiskFullException">The drive filled up while a file was kept.</exception>
    public async Task<FetchedComponent> FetchAsync(ComponentRelease release, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        List<HeldFile> held = [];
        List<string> folders = [];
        foreach (ComponentDownload download in release.Downloads)
        {
            await HoldAsync(download, held, folders, progress, cancellationToken);
        }

        IReadOnlyDictionary<string, string> found = ComponentLayout.Match(release, held);
        await ComponentLayout.CheckAddonsAsync(release, found, cancellationToken);
        return new(release, found, folders);
    }

    // One download into the cache: a file as it is, an archive unpacked.
    private async Task HoldAsync(ComponentDownload download, List<HeldFile> held, List<string> folders, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        CachedFile file = await cache.DownloadAsync(download.Url, ComponentHosts.Allowlist, download.FileName, download.Verification, null, progress, cancellationToken);
        if (!download.IsArchive)
        {
            held.Add(new(download.FileName, file.Path));
            return;
        }

        string folder = await cache.UnpackAsync(file, null, null, cancellationToken);
        folders.Add(folder);
        held.AddRange(ComponentLayout.Unpacked(folder));
    }
}

/// <summary>A component's files in the download cache, checked and ready to be placed.</summary>
/// <param name="Release">What was resolved.</param>
/// <param name="Files">Each expected file's full path in the cache, by its <see cref="ExpectedFile.Path"/>; an optional file the release lacks is absent.</param>
/// <param name="Folders">The folders the archives unpacked to, for a component placed by folder (VORT's includes).</param>
public sealed record FetchedComponent(ComponentRelease Release, IReadOnlyDictionary<string, string> Files, IReadOnlyList<string> Folders);
