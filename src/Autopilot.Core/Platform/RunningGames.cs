namespace Autopilot.Core.Platform;

/// <summary>Which running processes belong to a game: their executable image lies inside one of its folders.</summary>
internal static class RunningGames
{
    /// <summary>
    /// The processes whose image lies inside any of <paramref name="folders"/>, compared by whole path
    /// parts, ignoring case, either slash; a process whose image cannot be read is never one of them.
    /// </summary>
    public static IReadOnlyList<RunningProcess> Inside(IEnumerable<RunningProcess> processes, IEnumerable<string> folders)
    {
        string[] searched = [.. folders];
        return [.. processes.Where(process => process.ImagePath is { } image && searched.Any(folder => PathParts.IsAtOrBelow(folder, image)))];
    }
}
