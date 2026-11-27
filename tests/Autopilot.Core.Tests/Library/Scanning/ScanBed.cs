using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Library;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Tests.Executables;
using Autopilot.Core.Tests.Library.Stores;
using Microsoft.Extensions.Time.Testing;

namespace Autopilot.Core.Tests.Library.Scanning;

/// <summary>
/// Game folders in a temp tree with real executable headers, the host's records and choices as
/// fakes, and a fake clock. Disposing deletes the tree.
/// </summary>
internal sealed class ScanBed : IDisposable
{
    private readonly StoreBed _bed = new();

    public ScanBed() => Choices = new FakeChoices(Clock);

    public FakeTimeProvider Clock { get; } = new();

    public FakeInstallRecords Records { get; } = new();

    public FakeChoices Choices { get; }

    public GameReading Reading => new(Choices, Records, new ApiSearch([]));

    public string PathOf(string relative) => Path.TrimEndingDirectorySeparator(_bed.PathOf(relative));

    public string Folder(string relative) => Path.TrimEndingDirectorySeparator(_bed.Folder(relative));

    /// <summary>An executable of <paramref name="megabytes"/> MB with these static imports; returns its full path.</summary>
    public string Exe(string relative, long megabytes = 1, ushort machine = TestPe.Amd64, params string[] imports)
    {
        string path = PathOf(relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using FileStream file = File.Create(path);
        new TestPe(machine) { Size = megabytes * GameFolder.Megabyte }.WithImports(imports).WriteTo(file);
        return path;
    }

    /// <summary>A file that is not an executable at all, named like one.</summary>
    public string NotAnExe(string relative) => _bed.WriteFile(relative, "not an executable");

    /// <summary>A file Windows refuses to open; the test is inconclusive where that cannot be done.</summary>
    public void Refuse(string relative) => _bed.Deny(relative);

    /// <summary>This tool's install record in a folder, written for these executables.</summary>
    public void Installed(string relative, params string[] executables) => Records.Add(PathOf(relative), InstallState.Installed, executables);

    public StoreGame Game(string name, string folder, GameSource source = GameSource.Steam, string? storeExe = null) =>
        new(name, PathOf(folder), source, storeExe is null ? null : PathOf(storeExe));

    public async Task<GameEntry> ReadAsync(StoreGame game, CancellationToken cancellationToken) =>
        (await GameReader.ReadAsync(game, Reading, cancellationToken)).Entry;

    /// <summary>A source listing these folders, each game named after its folder.</summary>
    public FakeSource Lists(GameSource source, params string[] folders) =>
        FakeSource.Listing(source, [.. folders.Select(folder => Game(Path.GetFileName(folder), folder, source))]);

    public async Task<GameRead> ReadWithNotesAsync(StoreGame game, CancellationToken cancellationToken) =>
        await GameReader.ReadAsync(game, Reading, cancellationToken);

    public async Task<AddResult> AddAsync(string path, CancellationToken cancellationToken, params GameEntry[] library) =>
        await ManualGames.AddAsync(path, library, Reading, cancellationToken);

    public ScanRequest Full(params IGameSource[] sources) => new(sources, [], [], Reading, Clock, null);

    public ScanRequest Quick(IReadOnlyList<GameEntry> known, params IGameSource[] sources) => new(sources, [], [], Reading, Clock, known);

    public void Dispose() => _bed.Dispose();
}
