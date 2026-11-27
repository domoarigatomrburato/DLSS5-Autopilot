using Autopilot.Core.Library.Emulators;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Library;

/// <summary>Brings a saved library back as a scan would find it today.</summary>
internal sealed class CacheReload(GameReading reading, TimeProvider clock)
{
    private readonly List<LibraryNote> _notes = [];

    /// <summary>
    /// A game whose folder is gone is dropped; one whose files changed, or whose executable Windows
    /// refuses to open, is read again; the rest come back as saved, with their emulator found again
    /// and the person's choices applied. Then one entry per executable, in the library's order;
    /// null when no game is left.
    /// </summary>
    public async Task<CachedLibrary?> RunAsync(IReadOnlyList<CachedEntry> saved, CancellationToken cancellationToken)
    {
        List<GameEntry> games = [];
        HashSet<GameEntry> readAgain = [with(ReferenceEqualityComparer.Instance)];
        foreach (CachedEntry game in saved.Where(game => DrivePaths.IsDirectory(game.Folder)))
        {
            GameEntry entry = game.ToEntry();
            if (!entry.Protected && GameStamp.Of(entry) == game.Stamp)
            {
                games.Add(Restored(entry));
            }
            else if (await ReadAgainAsync(entry, cancellationToken) is { } read)
            {
                games.Add(read);
                _ = readAgain.Add(read);
            }
        }

        List<GameEntry> arranged = LibraryOrder.Arrange(games, reading.Choices);
        return arranged.Count == 0 ? null : new CachedLibrary(arranged, [.. arranged.Where(readAgain.Contains)], _notes);
    }

    // As a scan finds it: the emulator by its one rule, then the person's choices.
    private GameEntry Restored(GameEntry entry)
    {
        GameEntry withEmulator = entry with { Emulator = entry.Executable is { } executable ? EmulatorProfiles.For(executable, entry.Source) : null };
        return ExecutableFacts.WithChoices(withEmulator, reading.Choices.ForFolder(entry.Folder));
    }

    // A folder added by hand is read as a full scan reads a remembered folder, and left out when it
    // no longer holds an executable; any other game as its source listed it.
    private async Task<GameEntry?> ReadAgainAsync(GameEntry entry, CancellationToken cancellationToken)
    {
        long started = clock.GetTimestamp();
        GameRead? read = entry.Source == GameSource.Manual
            ? await ManualGames.ReadFolderAsync(entry.Folder, reading, cancellationToken)
            : await GameReader.ReadAsync(GameReader.Listing(entry), reading, cancellationToken);
        if (read is null)
        {
            return null;
        }

        _notes.AddRange(read.Notes);
        TimeSpan took = clock.GetElapsedTime(started);
        if (took >= Scanner.SlowReading)
        {
            _notes.Add(new SlowRead(read.Entry.Name, took));
        }

        return read.Entry;
    }
}
