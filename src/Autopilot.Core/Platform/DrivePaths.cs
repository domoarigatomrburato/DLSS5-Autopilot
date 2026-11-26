namespace Autopilot.Core.Platform;

/// <summary>
/// Tests and lists folders on a drive path, where an I/O error means "not there": a card reader with
/// no card or a locked volume raises one from a plain test, and must not take a whole source down.
/// </summary>
internal static class DrivePaths
{
    /// <summary>Whether <paramref name="path"/> is an absolute path to an existing directory.</summary>
    public static bool IsDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            return Path.IsPathFullyQualified(path) && Directory.Exists(path);
        }
        catch (Exception e) when (IsNotThere(e))
        {
            return false;
        }
    }

    /// <summary>Whether <paramref name="path"/> is an existing file.</summary>
    public static bool IsFile(string path)
    {
        try
        {
            return File.Exists(path);
        }
        catch (Exception e) when (IsNotThere(e))
        {
            return false;
        }
    }

    /// <summary>The subfolders of a folder, sorted by name; none when it cannot be listed.</summary>
    public static IReadOnlyList<string> Subfolders(string folder) => Sorted(folder, Directory.EnumerateDirectories);

    /// <summary>The files directly in a folder, sorted by name; none when it cannot be listed.</summary>
    public static IReadOnlyList<string> Files(string folder) => Sorted(folder, Directory.EnumerateFiles);

    /// <summary>Whether an exception from a file system call is the way an absent or unready drive fails.</summary>
    public static bool IsNotThere(Exception e) =>
        e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException;

    private static List<string> Sorted(string folder, Func<string, IEnumerable<string>> enumerate)
    {
        try
        {
            List<string> entries = [.. enumerate(folder)];
            entries.Sort(StringComparer.OrdinalIgnoreCase);
            return entries;
        }
        catch (Exception e) when (IsNotThere(e))
        {
            return [];
        }
    }
}
