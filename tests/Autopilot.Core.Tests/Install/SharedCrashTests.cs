using Autopilot.Core.Install;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>
/// A failed or crashed install takes its registry values and layer files back out with its files; one over an earlier
/// install leaves that install's registrations and graphics preference as they were, or finishes the new one's release.
/// </summary>
[TestClass]
public sealed class SharedCrashTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_fresh_install_rolled_back_leaves_the_registry_and_the_layer_folder_as_they_were()
    {
        using TransactionBed bed = SharedSwitches.Fresh();
        string outside = bed.OutsideSnapshot();
        TreeSnapshot before = bed.Snapshot();

        InstallFailed failed = await SharedSwitches.FailAtCommitAsync(bed, _ => Task.FromResult(SharedSwitches.Fresh()), SharedSwitches.X, Ct);

        Assert.AreEqual(FailureOutcome.RolledBack, failed.Note.Outcome);
        before.AssertSame(bed.Snapshot());
        Assert.AreEqual(outside, bed.OutsideSnapshot());
    }

    [TestMethod]
    public async Task A_failed_update_leaves_the_previous_installs_registrations_and_preference()
    {
        using TransactionBed bed = await SharedSwitches.InstalledAsync(Ct);
        string outside = bed.OutsideSnapshot();
        InstallRecord x = await bed.RecordAsync(Ct);

        _ = await SharedSwitches.FailAtCommitAsync(bed, SharedSwitches.InstalledAsync, SharedSwitches.Newer, Ct);

        Assert.AreEqual(outside, bed.OutsideSnapshot());
        FullRecord.AssertSame(x, await bed.RecordAsync(Ct));
    }

    [TestMethod]
    public async Task A_failed_switch_puts_back_what_the_previous_install_registered()
    {
        using TransactionBed bed = await SharedSwitches.InstalledAsync(Ct);
        string outside = bed.OutsideSnapshot();

        _ = await SharedSwitches.FailAtCommitAsync(bed, SharedSwitches.InstalledAsync, SharedSwitches.Y, Ct);

        Assert.AreEqual(outside, bed.OutsideSnapshot());
    }

    [TestMethod]
    public async Task A_failed_update_takes_back_out_the_layer_and_the_preference_it_added()
    {
        using TransactionBed bed = await SharedSwitches.VulkanOnlyAsync(Ct);
        string outside = bed.OutsideSnapshot();
        InstallRecord before = await bed.RecordAsync(Ct);

        _ = await SharedSwitches.FailAtCommitAsync(bed, SharedSwitches.VulkanOnlyAsync, SharedSwitches.Newer, Ct);

        Assert.AreEqual(outside, bed.OutsideSnapshot());
        FullRecord.AssertSame(before, await bed.RecordAsync(Ct));
    }

    [TestMethod]
    public async Task A_crash_between_the_commit_save_and_the_release_is_finished_by_the_next_transaction()
    {
        using TransactionBed bed = await SharedSwitches.InstalledAsync(Ct);
        bool committed = await SharedSwitches.CrashYAsync(bed, await SharedSwitches.FirstReleaseAsync(Ct), Ct);
        string? releasing = await bed.ReleasingAsync(Ct);

        _ = await bed.RecoverAsync(Ct);

        Assert.IsTrue(committed, "the crash came after the commit's save");
        Assert.IsNotNull(releasing, "the commit's save says what is still to release");
        SharedSwitches.AssertY(bed, await bed.RecordAsync(Ct), "");
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)] // One install of X, a crashed switch and a recovery per step of the switch.
    public async Task A_crash_at_any_step_is_recovered_to_the_previous_install_or_the_new_one_registry_included()
    {
        int steps = (await SharedSwitches.MadeStepsAsync(Ct)).Count;
        for (int survives = 0; survives < steps; survives++)
        {
            await ReplayAsync(survives, Ct);
        }
    }

    private static async Task ReplayAsync(int survives, CancellationToken cancellationToken)
    {
        using TransactionBed bed = await SharedSwitches.InstalledAsync(cancellationToken);
        InstallRecord x = await bed.RecordAsync(cancellationToken);
        TreeSnapshot before = bed.Snapshot();
        string outside = bed.OutsideSnapshot();
        bool committed = await SharedSwitches.CrashYAsync(bed, survives, cancellationToken);

        _ = await bed.RecoverAsync(cancellationToken);

        string at = $"crash after {survives} steps";
        if (committed)
        {
            SharedSwitches.AssertY(bed, await bed.RecordAsync(cancellationToken), at);
            return;
        }

        Switches.AssertBackToX(before, bed, x, await bed.RecordAsync(cancellationToken), at);
        Assert.AreEqual(outside, bed.OutsideSnapshot(), at);
    }
}
