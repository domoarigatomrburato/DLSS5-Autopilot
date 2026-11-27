namespace Autopilot.Core.Platform;

/// <summary>
/// Paths compared the way Windows compares them, on every host: by whole parts, ignoring case,
/// with either slash as a separator, so <c>C:\Games\Foo-other</c> is not inside <c>C:\Games\Foo</c>.
/// Nothing is resolved on disk: <c>.</c> and <c>..</c> are taken away by their spelling.
/// </summary>
internal static class PathParts
{
    private static readonly char[] _separators = ['\\', '/'];

    /// <summary>The parts of a relative path as spelled, split at either slash, empty and <c>.</c> parts included.</summary>
    public static string[] Split(string relative) => relative.Split(_separators);

    /// <summary>
    /// Whether <paramref name="path"/> is a root, holding no part below it: a drive (<c>C:\</c>), a UNC
    /// server or share (<c>\\server\share</c>), or <c>/</c>.
    /// </summary>
    public static bool IsRoot(string path) => Parts(path).Count <= RootParts(path, Spelled(path));

    /// <summary>Whether <paramref name="path"/> is <paramref name="folder"/> or lies below it; both are absolute paths.</summary>
    public static bool IsAtOrBelow(string folder, string path) => RelativeParts(folder, path) is not null;

    /// <summary>
    /// The parts of <paramref name="path"/> below <paramref name="folder"/>, empty for the folder itself;
    /// null when <paramref name="path"/> is not at or below it. Both are absolute paths.
    /// </summary>
    public static IReadOnlyList<string>? RelativeParts(string folder, string path)
    {
        List<string> folderParts = Parts(folder);
        List<string> pathParts = Parts(path);
        return folderParts.Count <= pathParts.Count && folderParts.SequenceEqual(pathParts.Take(folderParts.Count), StringComparer.OrdinalIgnoreCase)
            ? pathParts[folderParts.Count..]
            : null;
    }

    /// <summary>
    /// The folder <paramref name="levels"/> parts above <paramref name="path"/>, spelled as <paramref name="path"/>
    /// spells it, with its trailing separator; null when <paramref name="path"/> has fewer parts.
    /// </summary>
    public static string? Above(string path, int levels)
    {
        string above = path;
        for (int level = 0; level < levels; level++)
        {
            int cut = above.TrimEnd(_separators).LastIndexOfAny(_separators);
            if (cut < 0)
            {
                return null;
            }

            above = above[..(cut + 1)];
        }

        return above;
    }

    // The root stays: "C:\.." is "C:\", and a UNC path keeps its server and share.
    private static List<string> Parts(string path)
    {
        string[] spelled = Spelled(path);
        int root = RootParts(path, spelled);
        List<string> parts = [];
        foreach (string part in spelled)
        {
            if (part == "..")
            {
                if (parts.Count > root)
                {
                    parts.RemoveAt(parts.Count - 1);
                }
            }
            else if (part != ".")
            {
                parts.Add(part);
            }
        }

        return parts;
    }

    private static string[] Spelled(string path) => path.Split(_separators, StringSplitOptions.RemoveEmptyEntries);

    // How many leading parts are the root: a UNC path's server and share, a drive, or none.
    private static int RootParts(string path, string[] spelled) =>
        path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal) ? 2
        : spelled is [{ } first, ..] && first.EndsWith(':', StringComparison.Ordinal) ? 1
        : 0;
}
