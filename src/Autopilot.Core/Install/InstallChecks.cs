using Autopilot.Core.Archives;
using Microsoft.Extensions.Logging.Abstractions;

namespace Autopilot.Core.Install;

/// <summary>
/// The checks a transaction makes before it writes anything: a record it can read and that belongs to
/// this game folder, and a plan whose every path stays inside the game folder, named once.
/// </summary>
internal static class InstallChecks
{
    /// <summary>Why the plan cannot run over <paramref name="recorded"/>; null when it can.</summary>
    public static InstallRefused? RefusalOf(InstallPlan plan, RecordedInstall? recorded)
    {
        InstallPaths paths = new(plan.GameFolder);
        if (RecordRefusal(paths, recorded) is { } refused)
        {
            return refused;
        }

        if (!paths.Contains(plan.InstallFolder) || !plan.Groups.SelectMany(group => group.Steps).All(step => IsInside(paths, step)))
        {
            return new(InstallRefusal.PathOutsideGameFolder);
        }

        return Conflicts(plan, paths, recorded) ? new(InstallRefusal.PlanConflict) : null;
    }

    /// <summary>Why the folder's record refuses any transaction: it cannot be read, or names a game folder outside <paramref name="paths"/>; null when it does not.</summary>
    public static InstallRefused? RecordRefusal(InstallPaths paths, RecordedInstall? recorded)
    {
        if (recorded is { Record: null } unreadable)
        {
            return new(InstallRefusal.RecordUnreadable) { Problem = unreadable.Problem };
        }

        return recorded is { GameFolder: { } recordedFolder } && !paths.Contains(recordedFolder)
            ? new(InstallRefusal.RecordOutsideGameFolder)
            : null;
    }

    private static bool IsInside(InstallPaths paths, PlanStep step)
    {
        try
        {
            return paths.Contains(step.FullIn(paths));
        }
        catch (UnsafePathException)
        {
            return false;
        }
    }

    // One path named twice, or kept when the previous install does not list it. The resolver logs the entries
    // leading outside the game folder; this check only reads the previous install.
    private static bool Conflicts(InstallPlan plan, InstallPaths paths, RecordedInstall? recorded)
    {
        var prior = PriorInstall.Of(recorded, paths, NullLogger.Instance);
        HashSet<string> named = [with(StringComparer.OrdinalIgnoreCase)];
        foreach (PlanStep step in plan.Groups.SelectMany(group => group.Steps))
        {
            string full = step.FullIn(paths);
            if (!named.Add(full) || (step is KeepEntry && prior.At(full) is null))
            {
                return true;
            }
        }

        return false;
    }
}
