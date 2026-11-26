namespace Autopilot.Core.Library.Stores;

/// <summary>The games one source has found so far, each resolved folder once: the first spelling wins.</summary>
internal sealed class FoundGames
{
    private readonly HashSet<string> _folders = [with(StringComparer.OrdinalIgnoreCase)];

    private readonly List<StoreGame> _games = [];

    public IReadOnlyList<StoreGame> Games => _games;

    /// <summary>Whether a game in the folder is already listed.</summary>
    public bool Has(string folder) => _folders.Contains(ResolvedPath.Of(folder));

    /// <summary>Adds the game unless its folder is already listed.</summary>
    public void Add(StoreGame game)
    {
        if (_folders.Add(ResolvedPath.Of(game.Folder)))
        {
            _games.Add(game);
        }
    }
}
