using Autopilot.Core.Platform;

namespace Autopilot.Core.Library.Emulators;

/// <summary>The executables in a search root and two levels of folders below it.</summary>
internal static class EmulatorWalk
{
    private const int Depth = 2;

    // A drive's root and Program Files hold hundreds of folders, each with a few more: this covers
    // them on a real machine and keeps a root full of ROM folders from turning into a crawl.
    private const int FolderCap = 2000;

    // Links are not followed: one can point back up the tree or at another drive.
    private static readonly EnumerationOptions _listing = new() { AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true };

    /// <summary>
    /// Every <c>.exe</c> directly in <paramref name="root"/> and in its folders down to two levels,
    /// level by level, skipping folders whose name starts with a dot and stopping after
    /// <see cref="FolderCap"/> folders. A folder that cannot be listed is skipped.
    /// </summary>
    public static IEnumerable<string> Executables(string root, CancellationToken cancellationToken)
    {
        List<string> level = [root];
        int listed = 0;
        for (int depth = 0; depth <= Depth; depth++)
        {
            List<string> below = [];
            foreach (string folder in level.TakeWhile(_ => listed++ < FolderCap))
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (string executable in List(folder, Directory.EnumerateFiles, "*.exe"))
                {
                    yield return executable;
                }

                below.AddRange(depth < Depth ? List(folder, Directory.EnumerateDirectories, "*").Where(sub => !Path.GetFileName(sub).StartsWith('.', StringComparison.Ordinal)) : []);
            }

            level = below;
        }
    }

    private static List<string> List(string folder, Func<string, string, EnumerationOptions, IEnumerable<string>> enumerate, string pattern)
    {
        try
        {
            List<string> entries = [.. enumerate(folder, pattern, _listing)];
            entries.Sort(StringComparer.OrdinalIgnoreCase);
            return entries;
        }
        catch (Exception e) when (DrivePaths.IsNotThere(e))
        {
            return [];
        }
    }
}
