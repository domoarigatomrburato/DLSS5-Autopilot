using Autopilot.Core.Archives;
using Autopilot.Core.Storage;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Net;

/// <summary>
/// The per-user download cache. A file is fetched once, checked against what its name says and the
/// caller's verification, and only then moved into place; the next request reuses it without asking
/// the server. Every entry is written under a cross-process lock, so the CLI and the app can run together.
/// </summary>
public sealed class DownloadCache
{
    private readonly string _cacheRoot;
    private readonly TimeProvider _time;
    private readonly Verifier _verifier;
    private readonly PartDownload _parts;

    public DownloadCache(NetClient client, string cacheRoot, ISignatureVerifier signatures, TimeProvider time, ILogger<DownloadCache> logger)
    {
        _cacheRoot = cacheRoot;
        _time = time;
        _verifier = new(client, signatures, logger);
        _parts = new(client, _verifier);
    }

    /// <summary><c>%LOCALAPPDATA%\invidia-claritatis\cache</c>, shared with <see cref="NetClient"/>'s API answers.</summary>
    public static string DefaultRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "invidia-claritatis", "cache");

    /// <summary>The cached file for <paramref name="url"/>, fetched into the cache first when it is missing or bad.</summary>
    /// <exception cref="UnsafePathException"><paramref name="fileName"/> is not a safe file name; nothing was asked.</exception>
    /// <exception cref="NetException">The download failed, its content is wrong, or its verification failed.</exception>
    /// <exception cref="DiskFullException">The drive filled up while the file was written; nothing was kept.</exception>
    public async Task<CachedFile> DownloadAsync(
        Uri url,
        HostAllowlist hosts,
        string fileName,
        Verification verification,
        FileRequest? options,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        SafePath.ValidateFileName(fileName);
        Verification expected = await _verifier.ResolveAsync(hosts, fileName, verification, cancellationToken);
        string entry = CacheKeys.FileEntry(_cacheRoot, url, fileName, expected);
        await using FileStream held = await CacheLock.AcquireAsync(CacheLock.PathFor(entry), _time, cancellationToken);
        return await ReuseAsync(entry, fileName, expected, progress, cancellationToken)
            ?? await SayingDiskFullAsync(
            PartDownload.PartOf(entry),
            () => _parts.FetchAsync(new(url, hosts, fileName, entry, expected), options, progress, cancellationToken));
    }

    /// <summary>
    /// The finished entry <see cref="DownloadAsync"/> would return, found without any request: a
    /// sums file is resolved only from an answer the cache already keeps. Null when there is none,
    /// or when it no longer checks out; such an entry is left in place, since nothing is written
    /// here. No lock is taken: a finished entry is never rewritten in place.
    /// </summary>
    /// <exception cref="UnsafePathException"><paramref name="fileName"/> is not a safe file name.</exception>
    /// <exception cref="NetException">The sums file's URL is not https or not on <paramref name="hosts"/>.</exception>
    public async Task<CachedFile?> FindAsync(Uri url, HostAllowlist hosts, string fileName, Verification verification, CancellationToken cancellationToken)
    {
        SafePath.ValidateFileName(fileName);
        if (await _verifier.ResolveKeptAsync(hosts, fileName, verification, cancellationToken) is not { } expected)
        {
            return null;
        }
        try
        {
            return await UsableAsync(CacheKeys.FileEntry(_cacheRoot, url, fileName, expected), fileName, expected, cancellationToken);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            // Unlocked: another process deleted the entry between the test and the read.
            return null;
        }
    }

    /// <summary>
    /// Deletes a finished download under its entry's lock, waiting for another process that holds
    /// it: the download was thrown away. An entry already gone is not an error.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="file"/> is not one of this cache's downloads.</exception>
    public async Task DeleteAsync(CachedFile file, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        string entry = Path.GetFullPath(file.Path);
        if (!CacheKeys.IsFileEntry(_cacheRoot, entry))
        {
            throw new ArgumentException($"{file.Path} is not a download in the cache at {_cacheRoot}", nameof(file));
        }
        await using FileStream held = await CacheLock.AcquireAsync(CacheLock.PathFor(entry), _time, cancellationToken);
        File.Delete(entry);
    }

    /// <summary>
    /// The folder <paramref name="file"/> unpacks to with these options, unpacked on first use and
    /// reused as it is afterwards.
    /// </summary>
    /// <exception cref="ArchiveException">The archive was refused; nothing was written.</exception>
    /// <exception cref="DiskFullException">The drive filled up while the archive was unpacked; nothing was kept.</exception>
    public async Task<string> UnpackAsync(CachedFile file, TreeOptions? options, ArchiveLimits? limits, CancellationToken cancellationToken)
    {
        string folder = Path.Combine(_cacheRoot, "unpacked", CacheKeys.Unpacked(file.Path, options, limits));
        await using FileStream held = await CacheLock.AcquireAsync(CacheLock.PathFor(folder), _time, cancellationToken);
        if (!Directory.Exists(folder))
        {
            _ = await SayingDiskFullAsync(
                PartDestination.PathFor(folder),
                () => ArchiveExtractor.ExtractTreeAsync(file.Path, folder, options, limits, cancellationToken));
        }
        return folder;
    }

    /// <summary>
    /// The readable names (the asset's own name, without the key) of the downloads kept in the
    /// cache that match <paramref name="searchPattern"/> (<c>*</c> and <c>?</c>, ignoring case), for
    /// a resolver whose every source failed. Nothing is fetched or checked: a name says what was
    /// kept, not that it still checks out.
    /// </summary>
    public IReadOnlyList<string> CachedNames(string searchPattern) => CacheKeys.KeptNames(_cacheRoot, searchPattern);

    // A finished entry that still checks out; a bad one is deleted.
    private async Task<CachedFile?> ReuseAsync(string entry, string fileName, Verification expected, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        if (await UsableAsync(entry, fileName, expected, cancellationToken) is not { } usable)
        {
            if (File.Exists(entry))
            {
                File.Delete(entry);
            }
            return null;
        }
        progress?.Report(new(usable.Size, usable.Size));
        return usable;
    }

    // A finished entry that exists and still checks out; null otherwise, with nothing changed.
    private async Task<CachedFile?> UsableAsync(string entry, string fileName, Verification expected, CancellationToken cancellationToken) =>
        File.Exists(entry) && await _verifier.IsUsableAsync(entry, fileName, expected, cancellationToken)
            ? new(entry, new FileInfo(entry).Length, expected)
            : null;

    // Writing into the cache: a full drive removes the .part file or folder and is said as such.
    private async Task<T> SayingDiskFullAsync<T>(string part, Func<Task<T>> write)
    {
        try
        {
            return await write();
        }
        catch (Exception full) when (DiskSpace.IsDiskFull(full))
        {
            if (Directory.Exists(part))
            {
                Directory.Delete(part, recursive: true);
            }
            else
            {
                File.Delete(part);
            }
            throw new DiskFullException(part, _cacheRoot, full);
        }
    }
}
