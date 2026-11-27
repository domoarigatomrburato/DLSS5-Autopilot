using Autopilot.Core.Archives;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Install;

/// <summary>
/// The paths an install record holds, read as untrusted: relative to the game folder, compared ignoring
/// case with either slash as a separator, and never followed out of the game folder.
/// </summary>
internal static class RecordPaths
{
    /// <summary>
    /// Whether <paramref name="relative"/> names a place inside the game folder: not rooted, no empty,
    /// <c>.</c> or <c>..</c> part, and a valid Windows name (<see cref="SafePath"/>'s rules).
    /// </summary>
    public static bool IsInside(string relative)
    {
        // SafePath drops empty and "." parts silently; a record holding one is not trusted.
        if (PathParts.Split(relative).Any(part => part is "" or "."))
        {
            return false;
        }

        try
        {
            _ = SafePath.Parts(relative);
            return true;
        }
        catch (UnsafePathException)
        {
            return false;
        }
    }

    /// <summary>The form a recorded path is compared in: <c>/</c> separators, to compare ignoring case.</summary>
    public static string Key(string relative) => relative.Replace('\\', '/');

    /// <summary>
    /// The game folder of the record in <paramref name="installFolder"/>, which says the install folder is
    /// <paramref name="recorded"/> below it: that many trailing parts are taken off. Null when
    /// <paramref name="recorded"/> is not a safe relative path, the trailing parts are not it, or the game
    /// folder would be a root (a drive, a UNC server or share), which would put the whole drive in reach.
    /// </summary>
    public static string? GameFolderOf(string installFolder, string recorded)
    {
        string folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installFolder));
        if (recorded.Length == 0)
        {
            return folder;
        }

        if (!IsInside(recorded))
        {
            return null;
        }

        string[] trailing = PathParts.Split(recorded);
        return PathParts.Above(folder, trailing.Length) is { } above
            && PathParts.RelativeParts(above, folder) is { } below
            && below.SequenceEqual(trailing, StringComparer.OrdinalIgnoreCase)
            && !PathParts.IsRoot(above)
            ? Path.TrimEndingDirectorySeparator(above)
            : null;
    }

    /// <summary>The key of <paramref name="path"/> relative to <paramref name="gameFolder"/>; null when it is not below it.</summary>
    public static string? KeyBelow(string gameFolder, string path) =>
        PathParts.RelativeParts(gameFolder, Path.GetFullPath(path)) is { Count: > 0 } parts ? string.Join('/', parts) : null;
}
