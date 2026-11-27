using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Storage;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>An install a crash left open is rolled back by the next transaction before anything else.</summary>
[TestClass]
public sealed class OpenRecordTests(TestContext testContext)
{
    private static readonly byte[] _first = GameFolders.Dll("first");
    private static readonly byte[] _second = GameFolders.Dll("second");

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task An_install_a_crash_left_open_is_rolled_back_before_the_next_one_runs()
    {
        using TransactionBed bed = new(GameFolders.Empty);
        await CrashMidInstallAsync(bed, Ct);

        InstallCommitted next = await bed.CommitAsync(bed.Plan(RouteId.Native, Plans.Place("second.dll", _second)), Ct);

        AssertRolledBackFirst(bed, next, await bed.EntriesAsync(Ct));
    }

    /// <summary>An install crashing after writing its first file, its record left open.</summary>
    private static async Task CrashMidInstallAsync(TransactionBed bed, CancellationToken cancellationToken)
    {
        bed.Steps = CrashPoints.After(3);
        _ = await bed.InstallAsync(bed.Plan(RouteId.Native, Plans.Place("first.dll", _first), Plans.Place("other.dll", _first)), cancellationToken);
        bed.Steps = NoWriteSteps.Instance;
        Assert.IsTrue(bed.Exists("first.dll"), "the crash came after the first file");
    }

    private static void AssertRolledBackFirst(TransactionBed bed, InstallCommitted next, string entries)
    {
        Assert.IsTrue(next.Recovered);
        Assert.IsFalse(bed.Exists("first.dll"), "the open install's file is taken back out");
        Assert.AreEqual("second.dll Written", entries);
    }
}
