using Autopilot.Core.Library.Stores;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Library;

/// <summary>What a scan keeps as it was and what it reads, once the sources have listed their games.</summary>
/// <param name="Kept">Known games kept as they were read (quick scan only).</param>
/// <param name="ToRead">Listed games to read.</param>
/// <param name="HandAdded">Folders added by hand to read after them (full scan only).</param>
internal sealed record ScanPlan(IReadOnlyList<GameEntry> Kept, IReadOnlyList<StoreGame> ToRead, IReadOnlyList<string> HandAdded)
{
    /// <summary>Asks the sources, in <see cref="GameSource"/> order (a full scan asks the full-scan-only ones too), and plans the reading.</summary>
    public static async Task<ScanPlan> MakeAsync(ScanRequest request, IProgress<ScanProgress>? progress, List<LibraryNote> notes, CancellationToken cancellationToken)
    {
        IEnumerable<IGameSource> sources = request.Known is null ? request.Sources.Concat(request.FullScanOnlySources) : request.Sources;
        IReadOnlyList<StoreGame> listed = await SourceListing.ListAsync([.. sources.OrderBy(source => source.Source)], progress, notes, cancellationToken);
        if (request.Known is not { } known)
        {
            return new ScanPlan([], listed, [.. Remembered(request.HandAddedFolders, listed)]);
        }

        List<GameEntry> kept = await QuickScan.KeptAsync(known, listed, request.Reading.Records, cancellationToken);
        return new ScanPlan(kept, [.. QuickScan.NotSeenBefore(known, listed)], []);
    }

    // Absolute, a directory, not already listed and not given twice; the rest is skipped without a
    // word, since the saved list is a file people edit.
    private static IEnumerable<string> Remembered(IReadOnlyList<string> folders, IReadOnlyList<StoreGame> listed)
    {
        HashSet<string> present = [with(StringComparer.OrdinalIgnoreCase), .. listed.Select(game => ResolvedPath.Of(game.Folder))];
        return folders
            .Where(DrivePaths.IsDirectory)
            .Select(Path.TrimEndingDirectorySeparator)
            .Where(folder => present.Add(ResolvedPath.Of(folder)));
    }
}
