using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Storage;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>A route switch cut short by a crash: the next transaction finishes what the record says was under way.</summary>
[TestClass]
public sealed class CrashTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_crash_between_the_two_commit_saves_is_finished_by_the_next_transaction()
    {
        using TransactionBed bed = await Switches.InstalledAsync(Ct);
        string held = await CrashAfterCommitAsync(bed, Ct);

        InstallCommitted next = await bed.CommitAsync(bed.Plan(RouteId.OptiScaler, new KeepEntry("dxgi.dll")), Ct);

        Assert.AreNotEqual("", held, "the crash came before the held files went");
        Assert.IsTrue(next.Recovered);
        Assert.AreEqual("", bed.HeldFiles());
    }

    [TestMethod]
    public async Task A_rollback_cut_short_by_a_crash_is_finished_by_the_next_transaction()
    {
        using TransactionBed bed = await Switches.InstalledAsync(Ct);
        InstallRecord x = await bed.RecordAsync(Ct);
        TreeSnapshot before = bed.Snapshot();
        await FailThenCrashAsync(bed, Ct);

        _ = await bed.RecoverAsync(Ct);

        Switches.AssertBackToX(before, bed, x, await bed.RecordAsync(Ct));
    }

    /// <summary>Installs Y with a crash right after its commit's first save; the held files left.</summary>
    private static async Task<string> CrashAfterCommitAsync(TransactionBed bed, CancellationToken cancellationToken)
    {
        InstallPlan y = Switches.Y(bed);
        bed.Steps = CrashPoints.After(await bed.CommitStepAsync(y, cancellationToken));
        _ = await bed.InstallAsync(y, cancellationToken);
        bed.Steps = NoWriteSteps.Instance;
        return bed.HeldFiles();
    }

    /// <summary>Installs Y with the drive filling at its commit, and a crash three steps into the rollback.</summary>
    private static async Task FailThenCrashAsync(TransactionBed bed, CancellationToken cancellationToken)
    {
        InstallPlan y = Switches.Y(bed);
        int commit = await bed.CommitStepAsync(y, cancellationToken);
        bed.Steps = new BothSteps(CrashPoints.After(commit + 3), DiskFullPoints.At(commit));
        _ = await bed.InstallAsync(y, cancellationToken);
    }
}
