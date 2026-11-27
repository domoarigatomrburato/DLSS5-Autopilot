using Autopilot.Core.Install;
using Autopilot.Core.Storage;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>What the transaction tells as it goes, and that it makes exactly the changes it resolved.</summary>
[TestClass]
public sealed class ProgressTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task Progress_names_the_journal_each_group_the_check_and_the_commit()
    {
        using TransactionBed bed = await Switches.InstalledAsync(Ct);
        List<InstallProgress> seen = [];
        bed.Progress = new Seen(seen);

        _ = await bed.CommitAsync(Switches.Y(bed), Ct);

        AssertNamed(seen);
    }

    [TestMethod]
    public async Task The_transaction_makes_exactly_the_steps_the_resolved_plan_lists()
    {
        using TransactionBed bed = await Switches.InstalledAsync(Ct);
        InstallPlan y = Switches.Y(bed);
        IReadOnlyList<WriteStep> resolved = await bed.ResolvedStepsAsync(y, Ct);
        var made = CrashPoints.After(int.MaxValue);
        bed.Steps = made;

        _ = await bed.CommitAsync(y, Ct);

        AssertMade(resolved, made.Made);
    }

    private static void AssertNamed(List<InstallProgress> seen)
    {
        Assert.AreEqual(
            "1 Recording what will change|2 Setting the previous install aside|3 ReShade|4 Runtimes|5 Checking the files written|6 Finishing the install",
            string.Join("|", seen.Select(step => $"{step.Step} {step.Name}")));
        Assert.IsTrue(seen.All(step => step.Count == 6));
    }

    private static void AssertMade(IReadOnlyList<WriteStep> resolved, IReadOnlyList<WriteStep> made)
    {
        CollectionAssert.AreEqual(resolved.ToArray(), made.ToArray());
        Assert.AreEqual("SaveRecord|Move|Create|CreateFolder|Delete", string.Join("|", made.Select(step => step.Kind).Distinct()), "the switch makes every kind of change but a copy");
    }

    /// <summary>Progress reported on the calling thread, in order.</summary>
    private sealed class Seen(List<InstallProgress> seen) : IProgress<InstallProgress>
    {
        public void Report(InstallProgress value) => seen.Add(value);
    }
}
