using Autopilot.Core.Archives;
using Autopilot.Core.Net;
using Autopilot.Core.Storage;
using Microsoft.Extensions.Logging.Testing;

namespace Autopilot.Core.Tests.Net;

/// <summary>A download cache over <see cref="TestBed"/>'s fake server, clock and throwaway cache folder.</summary>
internal sealed class CacheBed : IDisposable
{
    public const string Url = "https://example.test/tool.zip";
    public const string Name = "tool.zip";
    public const string SumsUrl = "https://example.test/SHA256SUMS";

    private readonly CancellationToken _cancellation;

    public CacheBed(TestContext context, NetOptions? options = null)
    {
        _cancellation = context.CancellationToken;
        Bed = new(context, options);
        Files = new(Bed.CacheRoot, Url);
        Cache = new(Bed.Client, Bed.CacheRoot, Signatures, Bed.Time, Log);
    }

    public TestBed Bed { get; }

    public CacheFiles Files { get; }

    public FakeSigner Signatures { get; } = new();

    public FakeLogger<DownloadCache> Log { get; } = new();

    public DownloadCache Cache { get; }

    public FakeServer Server => Bed.Server;

    /// <summary>How many requests reached the server.</summary>
    public int Requests => Server.Requests.Count;

    /// <summary>Answers the download's URL with each body in turn; the last one repeats.</summary>
    public CacheBed Serve(params byte[][] bodies)
    {
        foreach (byte[] body in bodies)
        {
            _ = Server.AnswerBytes(Url, body);
        }
        return this;
    }

    public Task<CachedFile> Download(Verification? verification = null, string name = Name, IProgress<DownloadProgress>? progress = null, CancellationToken? cancellation = null) =>
        Cache.DownloadAsync(new(Url), TestBed.Hosts, name, verification ?? CacheFixtures.ZipVerification, null, progress, cancellation ?? _cancellation);

    public Task<NetException> DownloadFails(Verification? verification = null, string name = Name, IProgress<DownloadProgress>? progress = null) =>
        Assert.ThrowsAsync<NetException>(() => Download(verification, name, progress));

    public Task<DiskFullException> DownloadFillsTheDrive(IProgress<DownloadProgress> progress) =>
        Assert.ThrowsAsync<DiskFullException>(() => Download(progress: progress));

    public Task<string> Unpack(CachedFile file, TreeOptions? options = null, ArchiveLimits? limits = null) =>
        Cache.UnpackAsync(file, options, limits, _cancellation);

    public void Dispose() => Bed.Dispose();
}

/// <summary>The cache's folders as the spec lays them out.</summary>
internal sealed class CacheFiles(string cacheRoot, string url)
{
    public string FilesFolder => Path.Combine(cacheRoot, "files");

    public string UnpackedFolder => Path.Combine(cacheRoot, "unpacked");

    /// <summary>The zip's entry: the first 32 hex digits of SHA-256 over the URL, a newline and the digest, then the name.</summary>
    public string ZipEntry => EntryFor(CacheFixtures.ZipDigest);

    /// <summary>The names of the files in the downloads folder, lock files left out.</summary>
    public string[] Cached =>
        Directory.Exists(FilesFolder)
            ? [.. Directory.GetFiles(FilesFolder).Where(path => !path.EndsWith(".lock", StringComparison.Ordinal)).Select(Path.GetFileName).OfType<string>()]
            : [];

    public static bool Holds(string path, byte[] content) => File.ReadAllBytes(path).AsSpan().SequenceEqual(content);

    public string EntryFor(string digest) =>
        Path.Combine(FilesFolder, CacheFixtures.DigestOf(System.Text.Encoding.UTF8.GetBytes(url + "\n" + digest))[..32] + "-" + CacheBed.Name);

    /// <summary>Holds an entry's lock the way another process would.</summary>
    public FileStream HoldLock(string entry)
    {
        _ = Directory.CreateDirectory(FilesFolder);
        return new(entry + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
}
