using System.Text.Json.Nodes;
using Autopilot.Core.Library;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Tests.Library.Scanning;

namespace Autopilot.Core.Tests.Library.Cache;

/// <summary>
/// Games in a temp tree, read as a scan reads them, and a library cache beside them, outside every
/// game folder. Files and folders can be aged, so a change shows in the stamp's whole seconds.
/// </summary>
internal sealed class CacheBed : IDisposable
{
    public static readonly CacheKey Key = new("2.0.0", 89);

    private static readonly DateTime _old = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly string[] _noCandidates = [];

    public ScanBed Games { get; } = new();

    public string CachePath => Games.PathOf("Cache/library.json");

    /// <summary>A Steam game named after its folder, with a DirectX 11 executable <c>Game.exe</c> in it.</summary>
    public string Game(string folder, string exe = "Game.exe", long megabytes = 1) =>
        Games.Exe($"{folder}/{exe}", megabytes, imports: "d3d11.dll");

    public async Task<GameEntry> ReadAsync(string folder, CancellationToken cancellationToken, GameSource source = GameSource.Steam, string? storeExe = null) =>
        await Games.ReadAsync(Games.Game(Path.GetFileName(folder), folder, source, storeExe), cancellationToken);

    /// <summary>Sets each file's or folder's time well in the past, as if nothing had touched it in years.</summary>
    public void Age(params string[] relative)
    {
        foreach (string path in relative.Select(Games.PathOf))
        {
            if (Directory.Exists(path))
            {
                Directory.SetLastWriteTimeUtc(path, _old);
            }
            else
            {
                File.SetLastWriteTimeUtc(path, _old);
            }
        }
    }

    public async Task<CacheSaveResult> SaveAsync(CancellationToken cancellationToken, params GameEntry[] games) =>
        await LibraryCache.SaveAsync(CachePath, Key, games, Games.Clock, cancellationToken);

    /// <summary>Saves through <paramref name="through"/>, which wraps the temporary file's stream.</summary>
    public async Task<CacheSaveResult> SaveThroughAsync(Func<Stream, Stream> through, CancellationToken cancellationToken, params GameEntry[] games) =>
        await LibraryCache.SaveAsync(CachePath, Key, games, through, Games.Clock, cancellationToken);

    public async Task<CachedLibrary?> LoadAsync(CancellationToken cancellationToken, CacheKey? key = null) =>
        await LibraryCache.LoadAsync(CachePath, key ?? Key, Games.Reading, Games.Clock, cancellationToken);

    /// <summary>The saved library, which the test expects to be usable.</summary>
    public async Task<CachedLibrary> LoadedAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken) ?? throw new AssertFailedException("the cache was not used");

    /// <summary>Every file in the cache's folder, temporary ones included.</summary>
    public string[] FilesBesideCache() => Directory.GetFiles(Path.GetDirectoryName(CachePath)!);

    /// <summary>Rewrites the saved file after <paramref name="edit"/> changed its JSON.</summary>
    public void Edit(Action<JsonObject> edit)
    {
        JsonObject file = JsonNode.Parse(File.ReadAllText(CachePath))!.AsObject();
        edit(file);
        File.WriteAllText(CachePath, file.ToJsonString());
    }

    /// <summary>The first saved game, as JSON, for an edit.</summary>
    public static JsonObject FirstGame(JsonObject file) => file["games"]![0]!.AsObject();

    /// <summary>The loaded library holds the saved games, in the library's order by name.</summary>
    public static void AssertSameLibrary(IReadOnlyList<GameEntry> saved, IReadOnlyList<GameEntry> loaded)
    {
        Assert.HasCount(saved.Count, loaded);
        foreach ((GameEntry expected, GameEntry actual) in saved.OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase).Zip(loaded))
        {
            AssertSame(expected, actual);
        }
    }

    /// <summary>The two entries are the same game with the same facts.</summary>
    public static void AssertSame(GameEntry expected, GameEntry actual)
    {
        CollectionAssert.AreEqual(expected.Candidates.ToArray(), actual.Candidates.ToArray());
        Assert.AreEqual(expected with { Candidates = _noCandidates }, actual with { Candidates = _noCandidates });
    }

    public void Dispose() => Games.Dispose();
}
