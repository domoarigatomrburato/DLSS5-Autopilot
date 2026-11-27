using System.Collections.Frozen;
using System.IO.Enumeration;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Library;

/// <summary>
/// Lists the <c>.exe</c> files of a game folder: breadth first, children in name order, so the
/// shallow files are always seen first and the same tree always gives the same answer. Only counts
/// and the token end it; no clock does.
/// </summary>
internal sealed class ExecutableWalk
{
    private const int MaxDepth = 5;
    private const int MaxExecutables = 400;
    private const int MaxFolders = 6000;

    // Redistributables, anti-cheat, extras and data folders hold no game; "host64" is where our own
    // install puts the 32-bit feeder helper. Any folder starting with '.' is skipped as well.
    private static readonly FrozenSet<string> _skipped = new[]
    {
        "_commonredist", "commonredist", "redist", "redistributable", "redistributables", "directx", "dotnet",
        "vcredist", "vc_redist", "easyanticheat", "easyanticheat_eos", "battleye", "punkbuster", "installers",
        "installer", "prerequisites", "prereq", "support", "docs", "manual", "soundtrack", "artbook", "extras",
        "dxsetup", "crashreportclient", "epicwebhelper", "thirdparty", "steamvr", "openvr", "__installer",
        "dotnetfx", "movies", "content", "data", "assets", "textures", "host64",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    // Hidden and system entries are executables and folders like any other.
    private static readonly EnumerationOptions _listing = new() { AttributesToSkip = 0 };

    private ExecutableWalk()
    {
    }

    public List<FileInfo> Executables { get; } = [];

    public int FoldersSearched { get; private set; }

    /// <summary>A cap ended the walk with something left unseen.</summary>
    public bool StoppedEarly { get; private set; }

    public static ExecutableWalk Run(string folder, CancellationToken cancellationToken)
    {
        ExecutableWalk walk = new();
        walk.Visit(folder, cancellationToken);
        return walk;
    }

    private void Visit(string folder, CancellationToken cancellationToken)
    {
        Queue<(string Path, int Depth)> pending = new([(folder, 0)]);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FoldersSearched == MaxFolders)
            {
                StoppedEarly = true;
                return;
            }

            (string path, int depth) = pending.Dequeue();
            FoldersSearched++;
            foreach (FileSystemInfo entry in Children(path))
            {
                if (entry is DirectoryInfo directory)
                {
                    if (depth < MaxDepth && Enters(directory))
                    {
                        pending.Enqueue((directory.FullName, depth + 1));
                    }
                }
                else if (!TryAdd(entry))
                {
                    return;
                }
            }
        }
    }

    // False once the executable cap is hit by one more file.
    private bool TryAdd(FileSystemInfo entry)
    {
        if (entry is not FileInfo file || file.Name.Length <= ".exe".Length || !file.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Executables.Count == MaxExecutables)
        {
            StoppedEarly = true;
            return false;
        }

        Executables.Add(file);
        return true;
    }

    private static bool Enters(DirectoryInfo directory) =>
        !directory.Name.StartsWith('.', StringComparison.Ordinal)
        && !_skipped.Contains(directory.Name)
        && !directory.Attributes.HasFlag(FileAttributes.ReparsePoint);

    // Only folders and .exe files are listed. A folder that cannot be listed (access denied, an I/O
    // error, gone) is skipped, never fatal.
    private static List<FileSystemInfo> Children(string path)
    {
        try
        {
            FileSystemEnumerable<FileSystemInfo> folderAndExecutables = new(path, (ref entry) => entry.ToFileSystemInfo(), _listing)
            {
                ShouldIncludePredicate = (ref entry) => entry.IsDirectory || entry.FileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase),
            };
            return [.. folderAndExecutables
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Name, StringComparer.Ordinal)];
        }
        catch (Exception e) when (DrivePaths.IsNotThere(e))
        {
            return [];
        }
    }
}
