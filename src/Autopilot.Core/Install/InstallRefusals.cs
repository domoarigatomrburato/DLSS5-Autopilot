using Autopilot.Core.Platform;
using Autopilot.Core.Resources;
using Autopilot.Core.Storage;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install;

/// <summary>
/// Phase 1 of an install (install.md, "Order of an install"): every check that can say no, read-only, all of
/// them reported rather than the first, with the warnings. Writes nothing, starts nothing, requests nothing:
/// the folder's access and the running processes are asked, never probed.
/// </summary>
/// <param name="records">Reads the folder's record.</param>
/// <param name="access">Answers whether the folder can be written.</param>
/// <param name="processes">Lists the running processes.</param>
/// <param name="space">Answers the drive's free space.</param>
/// <param name="logger">Where record entries leading outside the game folder are logged.</param>
internal sealed class InstallRefusals(InstallRecords records, IFolderAccess access, IProcesses processes, IFreeSpace space, ILogger logger)
{
    /// <summary>The registry and the layer folder a plan's layer and graphics preference steps are resolved against, as the transaction's.</summary>
    internal SharedPlaces? Shared { get; init; }

    /// <summary>Every blocker of <paramref name="request"/> and its warnings, with the plan made concrete when its record and paths allow.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public async Task<InstallCheck> CheckAsync(RefusalRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RecordedInstall? recorded = await records.FindAsync(request.InstallFolder, cancellationToken);
        InstallRefused? planRefusal = InstallChecks.RefusalOf(request.Plan, recorded, Shared);
        InstallPaths paths = new(request.Plan.GameFolder);
        var prior = PriorInstall.Of(planRefusal is null ? recorded : null, paths, logger);
        Task<IReadOnlyList<string>> warnings = InstallWarnings.ForAsync(request, prior, cancellationToken);
        List<InstallBlocker> blockers = await BeforeResolutionAsync(request, prior, planRefusal, cancellationToken);
        ResolvedInstall? resolved = planRefusal is null ? await ResolvedAsync(request.Plan, paths, prior, blockers, cancellationToken) : null;
        return new(blockers, [.. Unfinished(recorded), .. await warnings]) { Resolved = resolved };
    }

    // An open record: the transaction recovers it before this install, which the preview does not simulate.
    private static IEnumerable<string> Unfinished(RecordedInstall? recorded) => recorded?.State switch
    {
        InstallState.Installing => [Texts.Text("WarningUnfinishedInstall")],
        InstallState.Removing => [Texts.Text("WarningUnfinishedRemoval")],
        _ => [],
    };

    /// <summary>
    /// Why the install cannot go ahead while the game runs; null when nothing from its folders runs. Part of
    /// <see cref="CheckAsync"/>, and asked again just before the transaction, since gathering can take minutes.
    /// </summary>
    public InstallBlocker? GameRunning(string gameFolder, string installFolder) =>
        FolderRefusals.GameRunning(processes, Path.GetFullPath(gameFolder), Path.GetFullPath(installFolder));

    // The game, the route, the settings and the card; then the folder, the running game, the record and the paths.
    private async Task<List<InstallBlocker>> BeforeResolutionAsync(RefusalRequest request, PriorInstall prior, InstallRefused? planRefusal, CancellationToken cancellationToken)
    {
        List<InstallBlocker> blockers = [.. await RouteRefusals.CheckAsync(request, prior, cancellationToken)];
        blockers.AddIfAny(FolderRefusals.Access(access, request.Game, request.InstallFolder));
        blockers.AddIfAny(GameRunning(request.GameFolder, request.InstallFolder));
        blockers.AddIfAny(planRefusal is null ? null : PlanBlocker(planRefusal));
        return blockers;
    }

    private static InstallBlocker PlanBlocker(InstallRefused refused) => refused.Reason switch
    {
        InstallRefusal.RecordUnreadable => new(BlockerKind.Record, Texts.Text("RefusalRecordUnreadable")),
        InstallRefusal.RecordOutsideGameFolder => new(BlockerKind.Record, Texts.Text("RefusalRecordElsewhere")),
        InstallRefusal.SharedPlacesMissing => new(BlockerKind.Plan, Texts.Text("RefusalSharedPlaces")),
        _ => new(BlockerKind.Plan, Texts.Text("RefusalPlan")),
    };

    // The plan made concrete, and what it meets: a file it must read and cannot, files held, too little room. An
    // unwritable folder answers every lock check as held, so its own refusal says it, once.
    private async Task<ResolvedInstall?> ResolvedAsync(InstallPlan plan, InstallPaths paths, PriorInstall prior, List<InstallBlocker> blockers, CancellationToken cancellationToken)
    {
        bool writable = !blockers.Exists(blocker => blocker.Kind is BlockerKind.FolderDenied or BlockerKind.StoreFolder or BlockerKind.FolderMissing);
        ResolvedInstall resolved;
        try
        {
            resolved = await new InstallResolver(logger) { Shared = Shared }.ResolveAsync(plan, paths, prior, cancellationToken);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            blockers.AddIfAny(writable ? new(BlockerKind.Unreadable, Texts.Format("RefusalUnreadable", e.Message)) : null);
            return null;
        }

        if (writable)
        {
            blockers.AddRange(FolderRefusals.Locked(resolved));
            blockers.AddRange(FolderRefusals.FreeSpace(space, resolved));
        }

        return resolved;
    }
}

/// <summary>Adding a blocker when there is one.</summary>
internal static class BlockerLists
{
    public static void AddIfAny(this List<InstallBlocker> blockers, InstallBlocker? blocker)
    {
        if (blocker is not null)
        {
            blockers.Add(blocker);
        }
    }
}
