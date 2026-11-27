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
        await using FileStream held = await CacheLock.AcquireAsync(entry + ".lock", _time, cancellationToken);
        return await ReuseAsync(entry, fileName, expected, progress, cancellationToken)
            ?? await SayingDiskFullAsync(
            PartDownload.PartOf(entry),
            () => _parts.FetchAsync(new(url, hosts, fileName, entry, expected), options, progress, cancellationToken));
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
        await using FileStream held = await CacheLock.AcquireAsync(folder + ".lock", _time, cancellationToken);
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
        if (!File.Exists(entry))
        {
            return null;
        }
        if (!await _verifier.IsUsableAsync(entry, fileName, expected, cancellationToken))
        {
            File.Delete(entry);
            return null;
        }
        long size = new FileInfo(entry).Length;
        progress?.Report(new(size, size));
        return new(entry, size, expected);
    }

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
