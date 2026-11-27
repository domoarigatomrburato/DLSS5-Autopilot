using Autopilot.Core.Storage;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Storage;

/// <summary>A target another program holds for a moment, as an antivirus or an indexer does.</summary>
[TestClass]
public sealed class AtomicFileRetryTests(TestContext testContext) : AtomicFileTestBase(testContext)
{
    public static IEnumerable<object[]> HeldTargets =>
    [
        [new IOException("sharing violation", SharingViolation)],
        [new IOException("lock violation", LockViolation)],
        [new UnauthorizedAccessException("in use")],
    ];

    [TestMethod]
    [DynamicData(nameof(HeldTargets))]
    public async Task A_target_held_for_a_moment_is_replaced_once_it_is_let_go(Exception held)
    {
        await WriteAsync("old");
        RecordingTimers time = new();
        int tries = 0;

        await AtomicFile.WriteAsync(Target, Text("new"), check: null, time, (temporary, target) =>
        {
            if (++tries <= 2)
            {
                throw held;
            }
            File.Move(temporary, target, overwrite: true);
        }, Ct);

        Assert.AreEqual(3, tries);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100) }, time.Waits);
        Assert.AreEqual("new", await File.ReadAllTextAsync(Target, Ct));
        Assert.HasCount(1, FilesBesideTarget());
    }

    [TestMethod]
    public async Task A_target_that_stays_held_is_given_up_after_six_tries_and_kept_whole()
    {
        await WriteAsync("old");
        RecordingTimers time = new();
        int tries = 0;

        IOException failure = await Assert.ThrowsAsync<IOException>(() => AtomicFile.WriteAsync(Target, Text("new"), check: null, time, (_, _) =>
        {
            tries++;
            throw new IOException("sharing violation", SharingViolation);
        }, Ct));

        Assert.AreEqual(SharingViolation, failure.HResult);
        Assert.AreEqual(6, tries);
        Assert.HasCount(5, time.Waits);
        Assert.AreEqual("old", await File.ReadAllTextAsync(Target, Ct));
        Assert.HasCount(1, FilesBesideTarget());
    }

    [TestMethod]
    public async Task Any_other_failure_of_the_replace_is_not_tried_again()
    {
        await WriteAsync("old");
        RecordingTimers time = new();
        int tries = 0;

        _ = await Assert.ThrowsAsync<IOException>(() => AtomicFile.WriteAsync(Target, Text("new"), check: null, time, (_, _) =>
        {
            tries++;
            throw new IOException("the network share went away", BadNetworkPath);
        }, Ct));

        Assert.AreEqual(1, tries);
        Assert.IsEmpty(time.Waits);
        Assert.AreEqual("old", await File.ReadAllTextAsync(Target, Ct));
        Assert.HasCount(1, FilesBesideTarget());
    }

    [TestMethod]
    public async Task A_write_cancelled_while_the_target_is_held_stops_and_removes_the_temporary_file()
    {
        await WriteAsync("old");
        using CancellationTokenSource cancel = new();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => AtomicFile.WriteAsync(Target, Text("new"), check: null, new RecordingTimers(), (_, _) =>
        {
            cancel.Cancel();
            throw new IOException("sharing violation", SharingViolation);
        }, cancel.Token));

        Assert.AreEqual("old", await File.ReadAllTextAsync(Target, Ct));
        Assert.HasCount(1, FilesBesideTarget());
    }
}
