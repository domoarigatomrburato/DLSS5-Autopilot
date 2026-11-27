using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>A crash at any step of a route switch: the next transaction leaves the folder on the previous route or the new one, never between.</summary>
[TestClass]
public sealed class ReplayTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_crash_at_any_step_of_a_route_switch_is_recovered_to_the_previous_route_or_the_new_one()
    {
        using TransactionBed installed = await Switches.InstalledAsync(Ct);
        int steps = (await installed.MadeStepsAsync(Switches.Y(installed), Ct)).Count;
        for (int survives = 0; survives < steps; survives++)
        {
            await ReplayAsync(installed, survives, Ct);
        }
    }

    private static async Task ReplayAsync(TransactionBed installed, int survives, CancellationToken cancellationToken)
    {
        using TransactionBed bed = installed.Clone();
        InstallRecord x = await bed.RecordAsync(cancellationToken);
        TreeSnapshot before = bed.Snapshot();
        bed.Steps = CrashPoints.After(survives);
        _ = await bed.InstallAsync(Switches.Y(bed), cancellationToken);
        bool committed = await bed.RecordAsync(cancellationToken) is { State: RecordState.Installed, Route: RouteId.OptiScaler };

        _ = await bed.RecoverAsync(cancellationToken);

        AssertRecovered($"crash after {survives} steps", committed, before, bed, x, await bed.RecordAsync(cancellationToken));
    }

    private static void AssertRecovered(string at, bool committed, TreeSnapshot before, TransactionBed bed, InstallRecord x, InstallRecord record)
    {
        if (committed)
        {
            Switches.AssertY(bed, record, at);
        }
        else
        {
            Switches.AssertBackToX(before, bed, x, record, at);
        }
    }
}
