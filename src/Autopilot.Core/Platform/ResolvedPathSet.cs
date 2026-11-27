namespace Autopilot.Core.Platform;

/// <summary>Folders compared by their <see cref="ResolvedPath"/>, ignoring case: one folder reached two ways is in once.</summary>
internal sealed class ResolvedPathSet
{
    private readonly HashSet<string> _resolved = [with(StringComparer.OrdinalIgnoreCase)];

    public ResolvedPathSet(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            _ = Add(path);
        }
    }

    public ResolvedPathSet()
    {
    }

    /// <summary>Adds the path; false when it resolves to one already in.</summary>
    public bool Add(string path) => _resolved.Add(ResolvedPath.Of(path));

    public bool Contains(string path) => _resolved.Contains(ResolvedPath.Of(path));
}
