using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>Files written and gone by the check, as antivirus takes them: the install commits and warns, and the record still names them.</summary>
[TestClass]
public sealed class QuarantineTests(TestContext testContext)
{
    private static readonly byte[] _ours = GameFolders.Dll("ours");

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task Files_gone_by_the_check_are_named_in_a_warning_and_stay_on_the_record()
    {
        using TransactionBed bed = new(GameFolders.Empty);
        InstallPlan plan = bed.Plan(RouteId.Native, PlanBeds.Place("a.dll", _ours), PlanBeds.Place("b.addon64", _ours));
        QuarantineBeforeTheCheck(bed, await bed.CommitStepAsync(plan, Ct));

        InstallCommitted committed = await bed.CommitAsync(plan, Ct);

        AssertWarned(committed, await bed.EntriesAsync(Ct));
    }

    // The group's last save comes just before the check.
    private static void QuarantineBeforeTheCheck(TransactionBed bed, int commit) =>
        bed.Steps = new StepHook((step, _) => step == commit - 1, () => Quarantine(bed));

    private static void Quarantine(TransactionBed bed)
    {
        File.Delete(bed.Tree.PathOf("a.dll"));
        File.Delete(bed.Tree.PathOf("b.addon64"));
    }

    private static void AssertWarned(InstallCommitted committed, string entries)
    {
        string warning = committed.Warnings.Single();
        Assert.StartsWith("2 file(s) were written and are no longer there: ", warning, StringComparison.Ordinal);
        Assert.Contains("a.dll", warning);
        Assert.Contains("b.addon64", warning);
        Assert.Contains("antivirus", warning);
        Assert.AreEqual("a.dll Written|b.addon64 Written", entries);
    }
}
