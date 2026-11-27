using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>Uninstall stopped part way, by a held file or a crash, and finished by the next run as if it had not stopped.</summary>
[TestClass]
public sealed class UninstallResumeTests(TestContext testContext)
{
    private const string Addon = "dlss5-feed.addon64";

    private static readonly int[] _growingWaits = [400, 800, 1200, 1600];

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_file_another_program_holds_is_tried_with_growing_waits_then_said_and_left_on_the_record_as_stuck()
    {
        using TransactionBed bed = await UninstallBed.XInstalledAsync(Ct);
        _ = bed.Hold(Addon);

        UninstallFinished stuck = await bed.UninstallFinishedAsync(Ct);

        UninstallBed.AssertSaid(stuck, "could not remove: dlss5-feed.addon64 (in use - is the game or its launcher still running?)");
        CollectionAssert.AreEqual(_growingWaits, bed.WaitsInMilliseconds());
        Assert.AreEqual("dlss5-feed.addon64 Stuck", await bed.EntriesAsync(Ct));
        Assert.IsTrue(stuck is { Left: 1, Complete: false });
    }

    [TestMethod]
    public async Task Once_the_file_is_let_go_the_next_uninstall_finishes_the_job()
    {
        using TransactionBed bed = await UninstallBed.XInstalledAsync(Ct);
        HeldPaths held = bed.Hold(Addon);
        _ = await bed.UninstallFinishedAsync(Ct);
        held.Release(bed.Tree.PathOf(Addon));

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.IsTrue(finished.Complete && !bed.Exists(Addon) && bed.Holds("dxgi.dll", Switches.GameDxgi));
        Assert.IsNull(await bed.FindAsync(Ct));
    }

    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)] // Replays the uninstall twice per change it makes, each over a copy of the folder.
    public async Task A_crash_at_any_change_is_finished_by_the_next_uninstall_or_the_next_start_as_if_uninterrupted()
    {
        using TransactionBed installed = new(Switches.GameFiles);
        TreeSnapshot before = installed.Snapshot();
        _ = await installed.CommitAsync(Switches.X(installed), Ct);
        (int steps, TreeSnapshot uninterrupted) = await CrashReplays.UninterruptedAsync(installed, Ct);
        before.AssertSame(uninterrupted);

        for (int survives = 0; survives < steps; survives++)
        {
            await CrashReplays.CrashThenReplayAsync(installed, uninterrupted, survives, atStart: false, Ct);
            await CrashReplays.CrashThenReplayAsync(installed, uninterrupted, survives, atStart: true, Ct);
        }
    }

    [TestMethod]
    [DataRow("the game updated the file")]
    [DataRow("the store put the original back")]
    [DataRow("the backup is gone")]
    [DataRow("our file is gone")]
    public async Task A_crash_over_a_file_the_game_changed_since_ends_as_the_uninterrupted_uninstall(string since)
    {
        using TransactionBed installed = await UninstallBed.SwappedAsync(Ct);
        Change(installed, since);
        (int steps, TreeSnapshot uninterrupted) = await CrashReplays.UninterruptedAsync(installed, Ct);

        for (int survives = 0; survives < steps; survives++)
        {
            await CrashReplays.CrashThenReplayAsync(installed, uninterrupted, survives, atStart: false, Ct);
        }
    }

    private static void Change(TransactionBed bed, string since)
    {
        switch (since)
        {
            case "the game updated the file":
                bed.Write("dxgi.dll", UninstallBed.Updated);
                break;
            case "the store put the original back":
                bed.Write("dxgi.dll", Switches.GameDxgi);
                break;
            case "the backup is gone":
                bed.Delete("dxgi.dll" + KnownFiles.BackupSuffix);
                break;
            default:
                bed.Delete("dxgi.dll");
                break;
        }
    }
}
