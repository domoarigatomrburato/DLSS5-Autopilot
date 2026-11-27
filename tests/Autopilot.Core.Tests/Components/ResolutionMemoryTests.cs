using Autopilot.Core.Components;

namespace Autopilot.Core.Tests.Components;

/// <summary>One resolution per component per run, however many callers ask at once.</summary>
[TestClass]
public sealed class ResolutionMemoryTests(TestContext testContext)
{
    [TestMethod]
    public async Task Callers_asking_together_share_one_resolution()
    {
        ResolutionMemory<string, string> memory = new();
        TaskCompletionSource<string> answer = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int resolutions = 0;
        Task<string> Resolve(CancellationToken _)
        {
            resolutions++;
            return answer.Task;
        }

        Task<string> first = memory.GetAsync("reshade", Resolve, testContext.CancellationToken);
        Task<string> second = memory.GetAsync("reshade", Resolve, testContext.CancellationToken);
        answer.SetResult("6.8.0");

        Assert.AreEqual("6.8.0", await first);
        Assert.AreEqual("6.8.0", await second);
        Assert.AreEqual(1, resolutions);
    }

    [TestMethod]
    public async Task A_caller_that_stops_waiting_leaves_the_shared_resolution_running()
    {
        ResolutionMemory<string, string> memory = new();
        TaskCompletionSource<string> answer = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenSource impatient = new();
        Task<string> Resolve(CancellationToken _) => answer.Task;

        Task<string> gaveUp = memory.GetAsync("feeder", Resolve, impatient.Token);
        await impatient.CancelAsync();
        _ = await Assert.ThrowsAsync<TaskCanceledException>(() => gaveUp);
        Task<string> waiting = memory.GetAsync("feeder", _ => Task.FromResult("a second resolution"), testContext.CancellationToken);
        answer.SetResult("v2.0.0");

        Assert.AreEqual("v2.0.0", await waiting, "the first resolution is still the one kept");
    }

    [TestMethod]
    public async Task The_first_caller_cancelling_does_not_cancel_the_resolution_others_share()
    {
        ResolutionMemory<string, string> memory = new();
        TaskCompletionSource<string> answer = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenSource impatient = new();
        int resolutions = 0;
        Task<string> Resolve(CancellationToken token)
        {
            resolutions++;
            return answer.Task.WaitAsync(token);
        }

        Task<string> gaveUp = memory.GetAsync("optiscaler", Resolve, impatient.Token);
        Task<string> waiting = memory.GetAsync("optiscaler", Resolve, testContext.CancellationToken);
        await impatient.CancelAsync();
        _ = await Assert.ThrowsAsync<TaskCanceledException>(() => gaveUp);
        answer.SetResult("v0.9.0");

        Assert.AreEqual("v0.9.0", await waiting);
        Assert.AreEqual(1, resolutions);
    }

    [TestMethod]
    public async Task A_success_the_owner_does_not_keep_is_asked_again()
    {
        ResolutionMemory<string, string> memory = new(value => value != "pages");
        int resolutions = 0;
        Task<string> Resolve(CancellationToken _) => Task.FromResult(++resolutions == 1 ? "pages" : "api");

        Assert.AreEqual("pages", await memory.GetAsync("mirror", Resolve, testContext.CancellationToken));
        Assert.AreEqual("api", await memory.GetAsync("mirror", Resolve, testContext.CancellationToken));
        Assert.AreEqual("api", await memory.GetAsync("mirror", Resolve, testContext.CancellationToken));
        Assert.AreEqual(2, resolutions);
    }
}
