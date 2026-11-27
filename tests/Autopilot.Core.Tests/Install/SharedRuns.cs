using Autopilot.Core.Executables;
using Autopilot.Core.Install;
using Autopilot.Core.Tests.TestBeds;
using Microsoft.Extensions.Logging.Abstractions;

namespace Autopilot.Core.Tests.Install;

/// <summary>Installs and uninstalls of beds with shared places, as tests run them.</summary>
internal static class SharedRuns
{
    /// <summary>Installs <paramref name="steps"/> for a 64-bit game and expects a commit.</summary>
    public static Task<InstallCommitted> CommitAsync(this TransactionBed bed, CancellationToken cancellationToken, params PlanStep[] steps) =>
        bed.CommitAsync(bed.Plan(Bitness.Bits64, steps), cancellationToken);

    /// <summary>Installs <paramref name="steps"/> for a 32-bit game and expects a commit.</summary>
    public static Task<InstallCommitted> Commit32Async(this TransactionBed bed, CancellationToken cancellationToken, params PlanStep[] steps) =>
        bed.CommitAsync(bed.Plan(Bitness.Bits32, steps), cancellationToken);

    /// <summary>Installs <paramref name="steps"/> for a 64-bit game and expects a failure.</summary>
    public static Task<InstallFailed> FailAsync(this TransactionBed bed, CancellationToken cancellationToken, params PlanStep[] steps) =>
        bed.InstallAsAsync<InstallFailed>(bed.Plan(Bitness.Bits64, steps), cancellationToken);

    /// <summary>Installs <paramref name="steps"/> for a 64-bit game and expects a refusal; gives its reason.</summary>
    public static async Task<InstallRefusal> RefusalAsync(this TransactionBed bed, CancellationToken cancellationToken, params PlanStep[] steps) =>
        (await bed.InstallAsAsync<InstallRefused>(bed.Plan(Bitness.Bits64, steps), cancellationToken)).Reason;

    /// <summary>The plan resolved over the bed's record and its shared places, as the preview and the transaction resolve it.</summary>
    public static async Task<ResolvedInstall> ResolveAsync(this TransactionBed bed, InstallPlan plan, CancellationToken cancellationToken) =>
        await new InstallResolver(NullLogger.Instance) { Shared = bed.Shared }.ResolveAsync(plan, await bed.FindAsync(cancellationToken), cancellationToken);

    /// <summary>A bed beside <paramref name="bed"/> whose disk finds <paramref name="path"/> held by another program.</summary>
    public static TransactionBed BesideHolding(this TransactionBed bed, string path)
    {
        TransactionBed other = bed.Beside(GameFolders.Empty);
        other.Seam = new HeldPaths().Hold(path).Seam;
        return other;
    }
}
