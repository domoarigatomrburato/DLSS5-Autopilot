using Autopilot.Core.Platform;

namespace Autopilot.Core.Library.Stores;

/// <summary>The games one source has found so far, each resolved folder once: the first spelling wins.</summary>
internal sealed class FoundGames(GameSource source)
{
    private readonly ResolvedPathSet _folders = new();

    private readonly List<StoreGame> _games = [];

    public IReadOnlyList<StoreGame> Games => _games;

    /// <summary>Whether a game in the folder is already listed.</summary>
    public bool Has(string folder) => _folders.Contains(folder);

    /// <summary>Adds the game unless its folder is already listed.</summary>
    public void Add(StoreGame game)
    {
        if (_folders.Add(game.Folder))
        {
            _games.Add(game);
        }
    }

    /// <summary>
    /// Adds the game a store lists in <paramref name="folder"/> when that is a directory, named
    /// <paramref name="title"/> (trimmed) or, without one, after the folder.
    /// </summary>
    public void Add(string? title, string? folder, string? storeExecutable = null)
    {
        if (DrivePaths.IsDirectory(folder))
        {
            Add(new StoreGame(StoreFiles.NonEmpty(title) ?? StoreFiles.FolderName(folder!), folder!, source, storeExecutable));
        }
    }
}
