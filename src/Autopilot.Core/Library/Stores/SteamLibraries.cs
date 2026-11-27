using Autopilot.Core.Platform;
using ValveKeyValue;

namespace Autopilot.Core.Library.Stores;

/// <summary>Steam's library folders: its own folder plus every one <c>libraryfolders.vdf</c> lists.</summary>
internal static class SteamLibraries
{
    private const string SteamKey = @"Software\Valve\Steam";

    /// <summary>Steam's own folder: Steam's own value first, then the installer's, then the default folders; null when none is a directory.</summary>
    public static string? Root(IRegistry registry, HostFolders folders)
    {
        string?[] candidates =
        [
            registry.ReadString(RegistryRoot.CurrentUser, RegistryView.Registry64, SteamKey, "SteamPath"),
            registry.ReadString(RegistryRoot.CurrentUser, RegistryView.Registry64, SteamKey, "InstallPath"),
            registry.ReadString(RegistryRoot.LocalMachine, RegistryView.Registry32, SteamKey, "InstallPath"),
            folders.ProgramFilesX86 is { } x86 ? Path.Join(x86, "Steam") : null,
            folders.ProgramFiles is { } programs ? Path.Join(programs, "Steam") : null,
        ];
        return candidates.FirstOrDefault(DrivePaths.IsDirectory);
    }

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
