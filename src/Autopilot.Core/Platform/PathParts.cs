namespace Autopilot.Core.Platform;

/// <summary>
/// Paths compared the way Windows compares them, on every host: by whole parts, ignoring case,
/// with either slash as a separator, so <c>C:\Games\Foo-other</c> is not inside <c>C:\Games\Foo</c>.
/// Nothing is resolved on disk: <c>.</c> and <c>..</c> are taken away by their spelling.
/// </summary>
internal static class PathParts
{
    private static readonly char[] _separators = ['\\', '/'];

    /// <summary>Whether <paramref name="path"/> is <paramref name="folder"/> or lies below it; both are absolute paths.</summary>
    public static bool IsAtOrBelow(string folder, string path)
    {
        List<string> folderParts = Parts(folder);
        List<string> pathParts = Parts(path);
        return folderParts.Count <= pathParts.Count && folderParts.SequenceEqual(pathParts.Take(folderParts.Count), StringComparer.OrdinalIgnoreCase);
    }

    // The root stays: "C:\.." is "C:\", and a UNC path keeps its server and share.
    private static List<string> Parts(string path)
    {
        string[] spelled = path.Split(_separators, StringSplitOptions.RemoveEmptyEntries);
        int root = path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal) ? 2
            : spelled is [{ } first, ..] && first.EndsWith(':', StringComparison.Ordinal) ? 1
            : 0;
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
}
