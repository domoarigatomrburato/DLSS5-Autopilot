using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>A preview of a plan that registers something outside the game folder.</summary>
internal static class OutsideEffects
{
    /// <summary>The preview of a plan registering a Vulkan layer, over <paramref name="gameFolder"/>.</summary>
    public static InstallPreview Preview(string gameFolder) =>
        InstallPreview.Of(new InstallCheck([], []) { Resolved = new(new InstallPaths(gameFolder), gameFolder, Recorded.Install(), [new ResolvedGroup("Vulkan layer", [new OutsideAction()])]) });

    // An action changing something outside the game folder, as a registration does.
    private sealed record OutsideAction() : InstallAction("")
    {
        public override IEnumerable<RecordEntry> Journal => [];

        public override PreviewLists Preview => PreviewLists.None;

        public override RouteMessage? Outside => new("DiskFullPlace", "the Vulkan layer", "every Vulkan application of this account", "'uninstall' removes it");

        public override IReadOnlyList<DiskChange> Changes(InstallPaths paths) => [];
    }
}

/// <summary>What a transaction did to a folder, read from snapshots before and after.</summary>
/// <param name="Written">The files created or changed, besides the record and the backups.</param>
/// <param name="Removed">The files gone.</param>
/// <param name="BackedUpIntact">Whether each backed-up file sits under its backup name as it was.</param>
internal sealed record FolderChange(IReadOnlyList<string> Written, IReadOnlyList<string> Removed, bool BackedUpIntact)
{
    /// <summary>The change from <paramref name="before"/> to <paramref name="after"/>, where <paramref name="backedUp"/> were to be backed up.</summary>
    public static FolderChange Between(TreeSnapshot before, TreeSnapshot after, IEnumerable<string> backedUp)
    {
        var backups = backedUp.ToDictionary(path => path + OriginalFile.SuffixOf(BackupKind.Backup), path => path, StringComparer.OrdinalIgnoreCase);
        TreeChange change = before.Change(after);
        string[] written = [.. change.Added.Concat(change.Changed).Where(path => path != KnownFiles.InstallRecord && !backups.ContainsKey(path))];
        bool intact = backups.All(backup => after.HashOf(backup.Key) is { } kept && kept == before.HashOf(backup.Value));
        return new(written, change.Removed, intact);
    }
}

/// <summary>A preview, then the install it previewed, on a refusal bed.</summary>
internal static class PreviewRuns
{
    /// <summary>Records a previous install that placed <c>old.addon64</c>, then previews <see cref="PreviewScenes.Plan"/> and installs it.</summary>
    public static async Task<(InstallPreview Preview, FolderChange Change)> PreviewThenInstallAsync(this RefusalBed bed, CancellationToken cancellationToken)
    {
        PlantPrevious(bed);
        InstallPlan plan = PreviewScenes.Plan(bed);
        InstallPreview preview = await bed.PreviewAsync(bed.Request(plan), cancellationToken);
        TreeSnapshot before = bed.Bed.Snapshot();
        _ = await bed.Bed.InstallAsync(plan, cancellationToken);
        return (preview, FolderChange.Between(before, bed.Bed.Snapshot(), preview.BackedUp));
    }

    // The previous install's record, vouching for old.addon64.
    private static void PlantPrevious(RefusalBed bed) =>
        Recorded.Plant(bed.Tree.Root, Recorded.Install("", Recorded.Ours("old.addon64", Plans.Facts(PreviewScenes.Ours))));
}
