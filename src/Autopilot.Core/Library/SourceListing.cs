using Autopilot.Core.Library.Stores;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Library;

/// <summary>Asks each source in turn; one folder is one game, the first source's.</summary>
internal static class SourceListing
{
    public static async Task<IReadOnlyList<StoreGame>> ListAsync(
        IReadOnlyList<IGameSource> sources, IProgress<ScanProgress>? progress, List<LibraryNote> notes, CancellationToken cancellationToken)
    {
        ResolvedPathSet folders = new();
        List<StoreGame> games = [];
        foreach (IGameSource source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ScanningSource(source.Source));
            games.AddRange((await FindAsync(source, notes, cancellationToken)).Where(game => folders.Add(game.Folder)));
        }

        return games;
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
