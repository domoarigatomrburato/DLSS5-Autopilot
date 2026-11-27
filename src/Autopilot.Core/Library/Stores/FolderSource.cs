using Autopilot.Core.Platform;

namespace Autopilot.Core.Library.Stores;

/// <summary>
/// Game folders kept outside any launcher: one level of subfolders of the usual game folders at the
/// root of every fixed drive (not removable, not network). Named after the folder.
/// </summary>
public sealed class FolderSource(IDrives drives) : IGameSource
{
    // Localised names kept as data: a Turkish, Spanish, German, French or Italian games folder.
    private static readonly string[] _gameFolders = ["Games", "Game", "Oyunlar", "Juegos", "Spiele", "Jeux", "Giochi", "My Games", "PC Games", "Installed Games"];

    // The stores' own libraries, found in a games folder, are not games.
    private static readonly string[] _launcherLibraries =
    [
        "steamlibrary", "steamapps", "epic games", "gog galaxy", "ea games", "ubisoft", "xboxgames", "amazon games", "battle.net", "common", "riot games", "rockstar games",
    ];

    public GameSource Source => GameSource.Folder;

    public Task<IReadOnlyList<StoreGame>> FindAsync(CancellationToken cancellationToken)
    {
        FoundGames found = new(GameSource.Folder);
        foreach (string folder in Candidates())
        {
            cancellationToken.ThrowIfCancellationRequested();
            string name = Path.GetFileName(folder);
            if (!name.StartsWith('.', StringComparison.Ordinal) && !name.StartsWith('$', StringComparison.Ordinal) && !_launcherLibraries.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                found.Add(new(name, folder, GameSource.Folder, null));
            }
        }

        return Task.FromResult(found.Games);
    }

    private IEnumerable<string> Candidates() =>
        drives.List()
            .Where(drive => drive.Kind == DriveKind.Fixed)
            .SelectMany(drive => _gameFolders.Select(games => Path.Join(drive.Root, games)))
            .SelectMany(DrivePaths.Subfolders);
}
