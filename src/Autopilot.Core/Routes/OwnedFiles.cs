using System.Collections.Frozen;
using Autopilot.Core.Graphics;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Routes;

/// <summary>
/// Tells the game's files from this tool's: a file is ours when the install record lists it,
/// unless the game's original sits backed up beside it (a swap), which proves the game shipped it.
/// </summary>
internal sealed class OwnedFiles(string installFolder, IReadOnlyList<string> recorded)
{
    private readonly FrozenSet<string> _recorded = recorded.Select(Normal).ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the existing file at <paramref name="path"/> is the game's rather than this tool's.</summary>
    public bool IsGames(string path) =>
        DrivePaths.IsFile(path + KnownFiles.BackupSuffix)
        || !_recorded.Contains(Normal(Path.GetRelativePath(installFolder, path)));

    // Recorded with either kind of slash, possibly with a leading "./".
    private static string Normal(string relative)
    {
        string forward = relative.Replace('\\', '/');
        while (forward.StartsWith("./", StringComparison.Ordinal))
        {
            forward = forward[2..];
        }

        return forward;
    }
}
