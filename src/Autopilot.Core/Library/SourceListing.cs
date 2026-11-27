using Autopilot.Core.Library.Stores;

namespace Autopilot.Core.Library;

/// <summary>Asks each source in turn; one folder is one game, the first source's.</summary>
internal static class SourceListing
{
    public static async Task<IReadOnlyList<StoreGame>> ListAsync(
        IReadOnlyList<IGameSource> sources, IProgress<ScanProgress>? progress, List<LibraryNote> notes, CancellationToken cancellationToken)
    {
        FoundGames found = new();
        foreach (IGameSource source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ScanningSource(source.Source));
            foreach (StoreGame game in await FindAsync(source, notes, cancellationToken))
            {
                found.Add(game);
            }
        }

        return found.Games;
    }

    // One store failing must not take the others down, nor go unsaid.
    private static async Task<IReadOnlyList<StoreGame>> FindAsync(IGameSource source, List<LibraryNote> notes, CancellationToken cancellationToken)
    {
        try
        {
            return await source.FindAsync(cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            notes.Add(new SourceFailed(source.Source, e));
            return [];
        }
    }
}
