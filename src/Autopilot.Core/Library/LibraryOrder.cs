using Autopilot.Core.Library.Stores;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Library;

/// <summary>One entry per executable, then the order the library is shown in.</summary>
internal static class LibraryOrder
{
    /// <summary>
    /// Two entries with the same executable are listed once: Steam gives the library folder and the
    /// Rockstar launcher the <c>GTAIV</c> folder inside it. The first source wins, unless the other
    /// carries an install or a choice made by hand (both are kept by its folder and would be lost).
    /// Emulator games (an emulator profile, or found by the emulator search) share an emulator's
    /// executable and are never merged. Sorted by name ignoring
    /// case, then by folder.
    /// </summary>
    public static List<GameEntry> Arrange(IEnumerable<GameEntry> games, IGameChoices choices)
    {
        Dictionary<string, int> at = [with(StringComparer.OrdinalIgnoreCase)];
        List<GameEntry> once = [];
        foreach (GameEntry game in games.OrderBy(game => game.Source))
        {
            if (game.Executable is null || game.Emulator is not null || game.Source == GameSource.Emulator)
            {
                once.Add(game);
            }
            else if (ResolvedPath.Of(game.Executable) is var executable && at.TryGetValue(executable, out int index))
            {
                once[index] = Weight(game, choices) > Weight(once[index], choices) ? game : once[index];
            }
            else
            {
                at[executable] = once.Count;
                once.Add(game);
            }
        }

        return [.. once.OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase).ThenBy(game => game.Folder, StringComparer.OrdinalIgnoreCase)];
    }

    // An install outweighs a choice, as in the reference; either outweighs nothing.
    private static int Weight(GameEntry game, IGameChoices choices) =>
        (game.Install is null ? 0 : 2) + (choices.ForFolder(game.Folder)?.SetsApiOrBitness == true ? 1 : 0);
}
