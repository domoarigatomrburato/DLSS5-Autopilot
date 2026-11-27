using System.Net;
using System.Text;
using Autopilot.Core.Components;
using Autopilot.Core.Components.GitHub;
using Autopilot.Core.Components.Mirror;
using Autopilot.Core.Tests.Components.GitHub;
using Autopilot.Core.Tests.Components.Mirror;

namespace Autopilot.Core.Tests.Components;

/// <summary>A run is one job: it shares each answer among the job's installs, asks again in the next job, and stops with the job.</summary>
[TestClass]
public sealed class ComponentRunTests(TestContext testContext)
{
    private static readonly string _list = ReleasesBed.ApiList(5);

    [TestMethod]
    public async Task Two_installs_in_one_run_share_one_answer()
    {
        using ComponentsBed bed = Redirecting(new ComponentsBed(testContext));
        ComponentRun run = bed.NextRun();

        IReadOnlyList<BuildEntry>[] together = await Task.WhenAll(run.Nvidia.GetAsync(bed.Cancellation), run.Nvidia.GetAsync(bed.Cancellation));
        IReadOnlyList<BuildEntry> later = await run.Nvidia.GetAsync(bed.Cancellation);

        Assert.AreEqual(1, bed.Server.RequestsTo(MirrorBed.NvidiaLatest));
        Assert.IsNotEmpty(later);
        Assert.AreSame(together[0], together[1]);
    }

    [TestMethod]
    public async Task The_next_run_asks_again_and_shares_only_the_api_cache_on_disk()
    {
        using ComponentsBed bed = Redirecting(new ComponentsBed(testContext));
        bed.Server.Answer(_list, HttpStatusCode.OK, ReleaseFixtures.Fixture("dxvk-releases.json"));

        foreach (ComponentRun run in new[] { bed.NextRun(), bed.NextRun() })
        {
            _ = await run.Nvidia.GetAsync(bed.Cancellation);
            _ = await run.Releases.GetListAsync(ReleasesBed.Repo, 5, bed.Cancellation);
        }

        Assert.AreEqual(2, bed.Server.RequestsTo(MirrorBed.NvidiaLatest), "each run asks the redirect");
        Assert.AreEqual(1, bed.Server.RequestsTo(_list), "the API's answer is kept on disk for 6 hours, across runs");
    }

    [TestMethod]
    public async Task Cancelling_the_run_cancels_the_resolution_in_flight_for_every_caller()
    {
        using ComponentsBed bed = new(testContext);
        using CancellationTokenSource job = new();
        HeldList held = new(bed);
        ComponentRun run = bed.NextRun(job.Token);

        Task first = List(run, bed.Cancellation);
        Task second = List(run, bed.Cancellation);
        await held.Asked.WaitAsync(bed.Cancellation);
        await job.CancelAsync();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => first);
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => second);
        Assert.AreEqual(1, bed.Server.RequestsTo(_list));
    }

    [TestMethod]
    public async Task A_callers_own_token_stops_only_its_own_wait()
    {
        using ComponentsBed bed = new(testContext);
        using CancellationTokenSource impatient = new();
        HeldList held = new(bed);
        ComponentRun run = bed.NextRun();

        Task gaveUp = List(run, impatient.Token);
        Task<ReleaseListAnswer> waiting = List(run, bed.Cancellation);
        await held.Asked.WaitAsync(bed.Cancellation);
        await impatient.CancelAsync();
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => gaveUp);
        held.Release();

        Assert.IsNotEmpty((await waiting).Releases);
        Assert.AreEqual(1, bed.Server.RequestsTo(_list));
    }

    // The list several components pick from, read once per run.
    private static Task<ReleaseListAnswer> List(ComponentRun run, CancellationToken cancellation) =>
        run.Releases.GetListOnceAsync(GitHubRepository.Parse(ReleasesBed.Repo), 5, cancellation);

    private static ComponentsBed Redirecting(ComponentsBed bed)
    {
        _ = bed.Server.Redirect(MirrorBed.NvidiaLatest, MirrorFixtures.NvidiaLatestLocation());
        return bed;
    }

    /// <summary>The API's list request, held open until <see cref="Release"/> or its cancellation.</summary>
    private sealed class HeldList
    {
        private readonly TaskCompletionSource _asked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public HeldList(ComponentsBed bed)
        {
            byte[] list = Encoding.UTF8.GetBytes(ReleaseFixtures.Fixture("dxvk-releases.json"));
            _ = bed.Server.On(_list, async (_, token) =>
            {
                _asked.TrySetResult();
                await _released.Task.WaitAsync(token);
                return bed.Server.Respond(HttpStatusCode.OK, list, declaredLength: null);
            });
        }

        /// <summary>Completes when the request reached the server.</summary>
        public Task Asked => _asked.Task;

        public void Release() => _released.SetResult();
    }
}
