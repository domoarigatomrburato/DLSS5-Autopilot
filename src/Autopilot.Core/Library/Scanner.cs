using Autopilot.Core.Library.Stores;

namespace Autopilot.Core.Library;

/// <summary>Builds the library: every source asked, each new game read, one entry per game.</summary>
public static class Scanner
{
    private static readonly TimeSpan _slow = TimeSpan.FromSeconds(1);

    /// <summary>
    /// A full scan when <see cref="ScanRequest.Known"/> is null: every source, every game read, then
    /// the folders added by hand. A quick scan otherwise: the full-scan-only sources are skipped, a
    /// known game is kept as it was read while it is still there, and only folders not seen before
    /// are read. One failing source is a note, not the end of the scan. Games are read one at a time,
    /// on the calling thread, so call it from a background thread.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task<LibraryScan> ScanAsync(ScanRequest request, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        List<LibraryNote> notes = [];
        ScanPlan plan = await ScanPlan.MakeAsync(request, progress, notes, cancellationToken);
        Reader reader = new(request.Reading, request.Clock, progress, notes, plan.ToRead.Count + plan.HandAdded.Count);
        List<GameEntry> games = [.. plan.Kept, .. await reader.ReadAllAsync(plan.ToRead, plan.HandAdded, cancellationToken)];
        return new LibraryScan(LibraryOrder.Arrange(games, request.Reading.Choices), notes);
    }

    // Reads the games one at a time, in order, reporting and timing each.
    private sealed class Reader(GameReading context, TimeProvider clock, IProgress<ScanProgress>? progress, List<LibraryNote> notes, int total)
    {
        private int _number;

        public async Task<List<GameEntry>> ReadAllAsync(IReadOnlyList<StoreGame> listed, IReadOnlyList<string> handAdded, CancellationToken cancellationToken)
        {
            List<GameEntry> games = [];
            foreach (StoreGame found in listed)
            {
                games.Add((await ReadAsync(found.Name, async token => await GameReader.ReadAsync(found, context, token), cancellationToken))!.Entry);
            }

            // A remembered folder that no longer holds an executable is left out without a word.
            foreach (string folder in handAdded)
            {
                if (await ReadAsync(Path.GetFileName(folder), token => ManualGames.ReadFolderAsync(folder, context, token), cancellationToken) is { } read)
                {
                    games.Add(read.Entry);
                }
            }

            return games;
        }

        private async Task<GameRead?> ReadAsync(string name, Func<CancellationToken, Task<GameRead?>> read, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ReadingGame(++_number, total, name));
            long started = clock.GetTimestamp();
            GameRead? result = await read(cancellationToken);
            TimeSpan took = clock.GetElapsedTime(started);
            if (result is not null)
            {
                notes.AddRange(result.Notes);
                if (took >= _slow)
                {
                    notes.Add(new SlowRead(result.Entry.Name, took));
                }
            }

            return result;
        }
    }
}
