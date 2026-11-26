using Autopilot.Core.Platform;
using ValveKeyValue;

namespace Autopilot.Core.Library.Stores;

/// <summary>Steam's library folders: its own folder plus every one <c>libraryfolders.vdf</c> lists.</summary>
internal static class SteamLibraries
{
    /// <summary>
    /// The libraries that are directories holding <c>steamapps\common</c>, each resolved folder once.
    /// Both shapes of the file are read: numbered keys holding the path, or numbered objects with a
    /// <c>path</c>.
    /// </summary>
    public static async Task<List<string>> ListAsync(string steamRoot, CancellationToken cancellationToken)
    {
        List<string> candidates = [steamRoot];
        if (await ValveText.ReadAsync(Path.Join(steamRoot, "steamapps", "libraryfolders.vdf"), cancellationToken) is { } folders)
        {
            candidates.AddRange(folders.Children.Where(child => int.TryParse(child.Key, out _)).Select(child => PathOf(child.Value)).OfType<string>());
        }

        HashSet<string> seen = [with(StringComparer.OrdinalIgnoreCase)];
        return [.. candidates.Where(library => DrivePaths.IsDirectory(Path.Join(library, "steamapps", "common")) && seen.Add(ResolvedPath.Of(library)))];
    }

    private static string? PathOf(KVObject entry) => entry.IsCollection ? ValveText.Text(entry, "path") : ValveText.Text(entry);
}
