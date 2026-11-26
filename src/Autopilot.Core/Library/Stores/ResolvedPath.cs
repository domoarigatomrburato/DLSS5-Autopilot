namespace Autopilot.Core.Library.Stores;

/// <summary>A folder's path after its junctions and symbolic links, so one folder reached two ways is one folder.</summary>
internal static class ResolvedPath
{
    // Deeper than any real chain of links; a loop stops here instead of running forever.
    private const int MaxLinks = 32;

    /// <summary>
    /// Follows every link along <paramref name="path"/>, part by part from the root, and the links
    /// along each target in turn. A part that cannot be read is kept as spelled.
    /// </summary>
    public static string Of(string path) => Of(path, MaxLinks);

    private static string Of(string path, int linksLeft)
    {
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        string current = Path.GetPathRoot(full) ?? "";
        foreach (string part in full[current.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Join(current, part);
            if (linksLeft > 0 && Target(current) is { } target)
            {
                current = Of(target, --linksLeft);
            }
        }

        return Path.TrimEndingDirectorySeparator(current);
    }

    private static string? Target(string path)
    {
        try
        {
            return new DirectoryInfo(path).LinkTarget is { } target ? Path.GetFullPath(target, Path.GetDirectoryName(path)!) : null;
        }
        catch (Exception e) when (Platform.DrivePaths.IsNotThere(e))
        {
            return null;
        }
    }
}
