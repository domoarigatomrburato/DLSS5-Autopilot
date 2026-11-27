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
                (List<string> executables, List<string> subfolders) = List(folder);
                foreach (string executable in executables)
                {
                    yield return executable;
                }

                below.AddRange(depth < Depth ? subfolders.Where(sub => !Path.GetFileName(sub).StartsWith('.', StringComparison.Ordinal)) : []);
            }

            level = below;
        }
    }

    // One listing per folder: its .exe files and its subfolders, each sorted by name.
    private static (List<string> Executables, List<string> Subfolders) List(string folder)
    {
        List<string> executables = [];
        List<string> subfolders = [];
        try
        {
            foreach (FileSystemInfo entry in new DirectoryInfo(folder).EnumerateFileSystemInfos("*", _listing))
            {
                if (entry is DirectoryInfo)
                {
                    subfolders.Add(Path.Join(folder, entry.Name));
                }
                else if (entry.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    executables.Add(Path.Join(folder, entry.Name));
                }
            }
        }
        catch (Exception e) when (DrivePaths.IsNotThere(e))
        {
            return ([], []);
        }

        executables.Sort(StringComparer.OrdinalIgnoreCase);
        subfolders.Sort(StringComparer.OrdinalIgnoreCase);
        return (executables, subfolders);
    }
}
