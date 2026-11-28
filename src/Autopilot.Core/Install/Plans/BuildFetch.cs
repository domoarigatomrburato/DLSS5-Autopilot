using Autopilot.Core.Components;
using Autopilot.Core.Components.Fetch;
using Autopilot.Core.Components.Mirror;
using Autopilot.Core.Net;

namespace Autopilot.Core.Install.Plans;

/// <summary>One gather's fetching: a component or a build's file into the download cache past every check, and each gathered file's facts taken once.</summary>
/// <param name="mirror">The mirror's build lists, NVIDIA's in front.</param>
/// <param name="files">Fetches a resolved component or a build into the cache, checked.</param>
/// <param name="gathered">What the gather has so far.</param>
/// <param name="progress">Where downloads report.</param>
/// <param name="cancellation">The gather's token.</param>
internal sealed class BuildFetch(MirrorCatalog mirror, ComponentFiles files, GatheredFiles gathered, IProgress<DownloadProgress>? progress, CancellationToken cancellation)
{
    private Task<BuildCatalog>? _catalog;

    public GatheredFiles Gathered => gathered;

    /// <summary>The build lists, read once per gather.</summary>
    public Task<BuildCatalog> CatalogAsync() => _catalog ??= mirror.GetAsync(cancellation);

    /// <summary>A resolved component's files in the cache, checked.</summary>
    public Task<FetchedComponent> FetchAsync(ComponentRelease release) => files.FetchAsync(release, progress, cancellation);

    /// <summary>
    /// <paramref name="entry"/>'s <paramref name="file"/> in the cache, past every check (<see cref="ComponentFiles.FetchBuildAsync"/>). A
    /// file failing its own check throws <see cref="NetException"/> with a <see cref="VerificationFailedDetail"/>, as
    /// <see cref="DlssnrBuilds.ChooseAsync"/> expects.
    /// </summary>
    public Task<string> FetchAsync(BuildEntry entry, BuildCatalog catalog, string file) => files.FetchBuildAsync(entry, file, catalog.Source, progress, cancellation);

    /// <summary>Records <paramref name="entry"/>'s <paramref name="file"/> at <paramref name="path"/> as gathered, with its label and where the list came from.</summary>
    public async Task AddBuildAsync(BuildEntry entry, BuildCatalog catalog, string file, string path)
    {
        string id = MirrorFamilies.IdOf(entry.Family);
        gathered.Add(id, file, path, await FactsAsync(entry.Contents, path, cancellation), null);
        gathered.Version(id, entry.Label);
        gathered.Note(catalog.Source.Notice);
    }

    /// <summary>Records the file at <paramref name="path"/> as <paramref name="id"/>'s <paramref name="file"/>, hashed.</summary>
    public async Task AddAsync(string id, string file, string path) => gathered.Add(id, file, path, await FactsAsync(null, path, cancellation), null);

    /// <summary>Records the small file at <paramref name="path"/> (a layer manifest) as <paramref name="id"/>'s <paramref name="file"/>, read once for its bytes and facts.</summary>
    public async Task AddContentAsync(string id, string file, string path)
    {
        byte[] bytes = await File.ReadAllBytesAsync(path, cancellation);
        gathered.Add(id, file, path, FileFacts.Of(bytes), bytes);
    }

    /// <summary>
    /// The facts of the file at <paramref name="path"/>: from the digest its contents check pinned (<paramref name="contents"/>,
    /// already proved) and its length, else hashed.
    /// </summary>
    /// <exception cref="FileNotFoundException">The file is gone.</exception>
    internal static async Task<FileFacts> FactsAsync(Verification? contents, string path, CancellationToken cancellationToken) =>
        contents is Sha256Verification pinned
            ? new(pinned.Digest, new FileInfo(path).Length)
            : await FileFacts.OfAsync(path, cancellationToken) ?? throw new FileNotFoundException($"{path} is gone", path);
}
