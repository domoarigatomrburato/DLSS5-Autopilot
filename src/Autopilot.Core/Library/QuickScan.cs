using Autopilot.Core.Install;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Library;

/// <summary>Which known games a quick scan keeps as they were read, and which listed folders it reads.</summary>
internal static class QuickScan
{
    /// <summary>
    /// The known games still on disk that a source still lists, that no store lists in the first place
    /// (an emulator, a folder added by hand), or that hold an install of ours, so they can be uninstalled.
    /// A game a store listed before and lists no more is dropped: a removed game often leaves its folder.
    /// </summary>
    public static async Task<List<GameEntry>> KeptAsync(IReadOnlyList<GameEntry> known, IReadOnlyList<StoreGame> listed, IInstallRecords records, CancellationToken cancellationToken)
    {
        ResolvedPathSet seen = new(listed.Select(game => game.Folder));
        List<GameEntry> kept = [];
        foreach (GameEntry game in known)
        {
            if (DrivePaths.IsDirectory(game.Folder) && (StillWanted(game, seen) || await HoldsRecordAsync(game, records, cancellationToken)))
            {
                kept.Add(game);
            }
        }

        return kept;
    }

    /// <summary>The listed games whose folder no known game has.</summary>
    public static IEnumerable<StoreGame> NotSeenBefore(IReadOnlyList<GameEntry> known, IReadOnlyList<StoreGame> listed)
    {
        ResolvedPathSet have = new(known.Select(game => game.Folder));
        return listed.Where(game => !have.Contains(game.Folder));
    }

    private static bool StillWanted(GameEntry game, ResolvedPathSet seen) =>
        game.Source is GameSource.Emulator or GameSource.Manual || game.Install is not null || seen.Contains(game.Folder);

    // An install made since the game was read is not in its entry; the folders are asked again.
    private static async Task<bool> HoldsRecordAsync(GameEntry game, IInstallRecords records, CancellationToken cancellationToken)
    {
        string?[] folders = [game.InstallFolder, game.Executable is { } executable ? Path.GetDirectoryName(executable) : null, game.Folder];
        foreach (string folder in folders.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (await records.FindAsync(folder, cancellationToken) is not null)
            {
                return true;
            }
        }

        return false;
    }
}
