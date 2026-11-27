using System.Collections.Frozen;
using Autopilot.Core.Graphics;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Routes;

/// <summary>
/// Finds an RTX Remix runtime: a <c>.trex</c> folder holding <c>d3d9.dll</c>. Remix sits where the
/// game loads d3d9.dll from, beside the executable or a folder or two below it.
/// </summary>
internal static class RemixRuntime
{
    private const string Folder = ".trex";

    private const string RuntimeDll = "d3d9.dll";

    // Folders searched for a .trex: the start folder and two levels below it, so a .trex lies at most
    // three levels deep. Bounded to 2000 folders listed besides, as a library scan runs this per game.
    private const int Levels = 3;
    private const int MaxFolders = 2000;

    // Never entered, compared ignoring case, and neither is any other folder starting with '.'.
    private static readonly FrozenSet<string> _skipped = new[]
    {
        "content", "paks", "saved", "logs", "movies", "sounds", "music", "videos", "localization", "shadercache",
        "derivedcache", "cache", "textures", "maps", "levels", "audio", "data", "assets", "mods", "screenshots",
        "steamapps", "redist", "_commonredist", "rtx-remix", "rtx_comp", "common", "update", "pc",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>The first <c>.trex</c> found under the folders, tried in order, or null.</summary>
    public static string? Find(IEnumerable<string> folders, CancellationToken cancellationToken) =>
        folders.DistinctBy(RuntimeWalk.Normal, StringComparer.OrdinalIgnoreCase)
            .Select(folder => Find(folder, cancellationToken))
            .FirstOrDefault(found => found is not null);

    /// <summary>The <c>.trex</c> folder under <paramref name="folder"/>, breadth first, links not followed; or null.</summary>
    public static string? Find(string folder, CancellationToken cancellationToken)
    {
        List<string> level = [folder];
        int listed = 0;
        for (int depth = 0; depth < Levels && level.Count > 0; depth++)
        {
            List<string> below = [];
            foreach (string current in level.TakeWhile(_ => listed++ < MaxFolders))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string runtime = Path.Join(current, Folder);
                if (DrivePaths.IsFile(Path.Join(runtime, RuntimeDll)))
                {
                    return runtime;
                }

                below.AddRange(FolderListing.RealFolders(current, cancellationToken).Where(Enters));
            }

            level = below;
        }

        return null;
    }

    private static bool Enters(string folder)
    {
        string name = Path.GetFileName(folder);
        return !name.StartsWith('.', StringComparison.Ordinal) && !_skipped.Contains(name);
    }
}
