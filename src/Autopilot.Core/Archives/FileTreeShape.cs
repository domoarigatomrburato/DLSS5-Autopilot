namespace Autopilot.Core.Archives;

/// <summary>
/// The paths placed so far, ignoring case, with '/' separators: no path may be both a file and a
/// folder, since Windows cannot hold both.
/// </summary>
internal sealed class FileTreeShape
{
    private readonly HashSet<string> _files = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly HashSet<string> _folders = [with(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Records a file; false when its path is a folder, or one of its folders a file.</summary>
    public bool AddFile(string path) =>
        !_folders.Contains(path) && AddFolders(ParentOf(path)) && _files.Add(path);

    /// <summary>Records a folder and its parents; false when one of them is a file.</summary>
    public bool AddFolder(string path) => AddFolders(path);

    private bool AddFolders(string? folder)
    {
        for (string? current = folder; !string.IsNullOrEmpty(current); current = ParentOf(current))
        {
            if (_files.Contains(current))
            {
                return false;
            }

            if (!_folders.Add(current))
            {
                // Its parents were recorded with it.
                return true;
            }
        }

        return true;
    }

    private static string? ParentOf(string path)
    {
        int slash = path.LastIndexOf('/', StringComparison.Ordinal);
        return slash < 0 ? null : path[..slash];
    }
}
