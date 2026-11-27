using Autopilot.Core.Archives;
using Microsoft.Extensions.Logging.Abstractions;

namespace Autopilot.Core.Install;

/// <summary>
/// The checks a transaction and its preview make before anything is written: a record it can read and that belongs
/// to this game folder, a plan whose every path stays inside the game folder, named once, and the registry and
/// layer folder its layer and graphics preference steps need.
/// </summary>
internal static class InstallChecks
{
    /// <summary>
    /// Why the plan cannot run over <paramref name="recorded"/> with <paramref name="shared"/>; null when it can. Asked alike by
    /// the preview and the transaction.
    /// </summary>
    public static InstallRefused? RefusalOf(InstallPlan plan, RecordedInstall? recorded, SharedPlaces? shared)
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

        return Conflicts(plan, paths, recorded) ? new(InstallRefusal.PlanConflict) : SharedRefusal(plan, shared);
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

    /// <summary>
    /// Why the plan cannot run with <paramref name="shared"/>: it registers a layer with no registry or layer folder, or sets
    /// a graphics preference with no registry; null when it can. Such a step is never skipped silently.
    /// </summary>
    private static InstallRefused? SharedRefusal(InstallPlan plan, SharedPlaces? shared)
    {
        IEnumerable<PlanStep> steps = plan.Groups.SelectMany(group => group.Steps);
        bool unplaced = steps.Any(step => step switch
        {
            RegisterLayer => shared?.LayerFolder is null,
            SetGraphicsPreference => shared is null,
            _ => false,
        });
        return unplaced ? new(InstallRefusal.SharedPlacesMissing) : null;
    }

    private static bool IsInside(InstallPaths paths, PlanStep step)
    {
        try
        {
            return !step.NamesPath || paths.Contains(step.FullIn(paths));
        }
        catch (UnsafePathException)
        {
            return false;
        }
    }

    // One path named twice, or kept when the previous install does not list it; one layer, or one program's graphics
    // preference, named twice. The resolver logs the entries leading outside the game folder; this check only reads the
    // previous install.
    private static bool Conflicts(InstallPlan plan, InstallPaths paths, RecordedInstall? recorded)
    {
        var prior = PriorInstall.Of(recorded, paths, NullLogger.Instance);
        HashSet<string> named = [with(StringComparer.OrdinalIgnoreCase)];
        HashSet<string> shared = [with(StringComparer.OrdinalIgnoreCase)];
        foreach (PlanStep step in plan.Groups.SelectMany(group => group.Steps))
        {
            if (step.SharedKeyIn(paths) is { } key)
            {
                if (!shared.Add(key))
                {
                    return true;
                }

                continue;
            }

            string full = step.FullIn(paths);
            if (!named.Add(full) || (step is KeepEntry && prior.At(full) is null))
            {
                return true;
            }
        }

        return false;
    }
}
