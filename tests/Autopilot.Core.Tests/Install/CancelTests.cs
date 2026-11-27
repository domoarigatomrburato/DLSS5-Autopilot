using Autopilot.Core.Install;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>A cancel at any step: before the commit, the folder back as it was and the cancel rethrown; after it, the install finished.</summary>
[TestClass]
public sealed class CancelTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_cancel_at_any_step_rolls_back_before_the_commit_and_lets_it_finish_after()
    {
        using TransactionBed installed = await Switches.InstalledAsync(Ct);
        IReadOnlyList<Core.Storage.WriteStep> steps = await installed.MadeStepsAsync(Switches.Y(installed), Ct);
        int commit = TransactionSteps.CommitStep(steps);
        for (int at = 1; at <= steps.Count; at++)
        {
            await CancelAtAsync(installed, at, commit, Ct);
        }
    }

    private static async Task CancelAtAsync(TransactionBed installed, int at, int commit, CancellationToken cancellationToken)
    {
        using TransactionBed bed = installed.Clone();
        InstallRecord x = await bed.RecordAsync(cancellationToken);
        TreeSnapshot before = bed.Snapshot();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bed.Steps = new StepHook((step, _) => step == at, cancel.Cancel);

        OperationCanceledException? thrown = await CancelledAsync(bed, cancel.Token);

        AssertCancelled(new(at, commit, cancel.Token), thrown, before, bed, x, await bed.RecordAsync(cancellationToken));
    }

    private static async Task<OperationCanceledException?> CancelledAsync(TransactionBed bed, CancellationToken token)
    {
        try
        {
            _ = await bed.InstallAsync(Switches.Y(bed), token);
            return null;
        }
        catch (OperationCanceledException cancelled)
        {
            return cancelled;
        }
    }

    private static void AssertCancelled(CancelPoint point, OperationCanceledException? thrown, TreeSnapshot before, TransactionBed bed, InstallRecord x, InstallRecord record)
    {
        string at = $"cancel at step {point.At} of a commit at {point.Commit}";
        if (thrown is null)
        {
            Assert.IsGreaterThanOrEqualTo(point.Commit, point.At, at);
            Switches.AssertY(bed, record, at);
        }
        else
        {
            Assert.AreEqual(point.Token, thrown.CancellationToken, at);
            Switches.AssertBackToX(before, bed, x, record, at);
            Assert.AreEqual(FailureKind.Cancelled, bed.Note?.Kind, at);
        }
    }

    private sealed record CancelPoint(int At, int Commit, CancellationToken Token);
}
