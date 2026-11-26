using Autopilot.Core.Graphics;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Library.Stores;

/// <summary>Whether a folder under <c>steamapps\common</c> is a game, or what an uninstall left behind.</summary>
internal static class SteamFolder
{
    // One listing, never a walk: a leftover folder can be huge.
    private const int ListingCap = 400;

    /// <summary>
    /// A folder holding an install record of ours always counts, so it can be uninstalled. Otherwise
    /// Steam's own tools never count; a fully installed manifest does; and a folder with no manifest,
    /// or one not yet complete, counts only with an executable directly in it.
    /// </summary>
    public static bool Counts(string folder, SteamManifest? manifest)
    {
        if (DrivePaths.IsFile(Path.Join(folder, KnownFiles.InstallRecord)))
        {
            return true;
        }

        if (manifest is { IsTool: true })
        {
            return false;
        }

        return manifest is { FullyInstalled: true } || HoldsExecutable(folder);
    }

    // A folder too full to list in one go, or one that cannot be listed, stays in the list.
    private static bool HoldsExecutable(string folder)
    {
        try
        {
            int seen = 0;
            foreach (string entry in Directory.EnumerateFileSystemEntries(folder))
            {
                if (entry.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || ++seen >= ListingCap)
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception e) when (DrivePaths.IsNotThere(e))
        {
            return true;
        }
    }
}
