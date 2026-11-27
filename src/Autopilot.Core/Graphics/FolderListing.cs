using Autopilot.Core.Platform;

namespace Autopilot.Core.Graphics;

/// <summary>Lists folders for the decision, treating a folder that cannot be listed as empty.</summary>
internal static class FolderListing
{
    /// <summary>The files directly in a folder; none when it cannot be listed.</summary>
    public static IReadOnlyList<string> Files(string folder, CancellationToken cancellationToken) =>
        List(folder, int.MaxValue, Directory.EnumerateFiles, cancellationToken);

    /// <summary>Up to <paramref name="limit"/> subfolders of a folder, sorted by name.</summary>
    public static IReadOnlyList<string> Folders(string folder, int limit, CancellationToken cancellationToken) =>
        List(folder, limit, Directory.EnumerateDirectories, cancellationToken);

    // Sorted before the limit applies, as the reference sorted its listing and took the first entries.
    private static List<string> List(string folder, int limit, Func<string, IEnumerable<string>> enumerate, CancellationToken cancellationToken)
    {
        List<string> entries = [];
        try
        {
            foreach (string entry in enumerate(folder))
            {
                cancellationToken.ThrowIfCancellationRequested();
                entries.Add(entry);
            }
        }
        catch (Exception e) when (DrivePaths.IsNotThere(e))
        {
            return [];
        }

        entries.Sort((a, b) => string.CompareOrdinal(Path.GetFileName(a), Path.GetFileName(b)));
        return entries.Count > limit ? entries[..limit] : entries;
    }
}
