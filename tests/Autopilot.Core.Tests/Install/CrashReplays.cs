using Autopilot.Core.Install;
using Autopilot.Core.Storage;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>An uninstall crashed at a chosen change, over a copy of the folder, then replayed.</summary>
internal static class CrashReplays
{
    // How many changes an uninterrupted uninstall makes, over a copy, and the folder it leaves.
    public static async Task<(int Steps, TreeSnapshot Snapshot)> UninterruptedAsync(TransactionBed installed, CancellationToken cancellationToken)
    {
        using TransactionBed copy = installed.Clone();
        var made = CrashPoints.After(int.MaxValue);
        copy.Steps = made;
        _ = await copy.UninstallFinishedAsync(cancellationToken);
        return (made.Made.Count, copy.Snapshot());
    }

    // At start only a removal part way is finished; a record the crash left untouched waits for the next uninstall.
    public static async Task CrashThenReplayAsync(TransactionBed installed, TreeSnapshot uninterrupted, int survives, bool atStart, CancellationToken cancellationToken)
    {
        using TransactionBed bed = installed.Clone();
        bed.Steps = CrashPoints.After(survives);
        _ = await bed.UninstallAsync(cancellationToken);
        if (atStart && await bed.FindAsync(cancellationToken) is { State: InstallState.Removing })
        {
            Assert.IsNull(await bed.RecoverAsync(cancellationToken));
        }
        else
        {
            bed.Steps = NoWriteSteps.Instance;
            _ = await bed.UninstallFinishedAsync(cancellationToken);
        }

        Assert.IsNull(uninterrupted.Difference(bed.Snapshot()), $"crash after {survives} changes, replayed {(atStart ? "at start" : "by uninstall")}");
    }
}
