using Autopilot.Core.Library;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Library.Cache;

/// <summary>Saving never takes the tool down: a failure is a line for the log, and leaves no temporary file.</summary>
[TestClass]
public sealed class CacheSaveTests(TestContext testContext) : IDisposable
{
    private readonly CacheBed _bed = new();

    private CancellationToken Ct => testContext.CancellationToken;

    public void Dispose() => _bed.Dispose();

    [TestMethod]
    public async Task A_full_drive_is_said_and_leaves_the_old_cache_whole_and_no_temporary_file()
    {
        _ = _bed.Game("Arena");
        GameEntry arena = await _bed.ReadAsync("Arena", Ct);
        _ = await _bed.SaveAsync(Ct, arena);
        string before = await File.ReadAllTextAsync(_bed.CachePath, Ct);

        CacheSaveResult result = await _bed.SaveThroughAsync(file => new FullDriveStream(file), Ct, arena, arena with { Name = "Again" });

        Assert.IsFalse(result.Saved);
        Assert.IsTrue(result.DriveFull);
        Assert.AreEqual("the library cache was not saved: the drive is full", result.Explain());
        CollectionAssert.AreEqual(new[] { _bed.CachePath }, _bed.FilesBesideCache());
        Assert.AreEqual(before, await File.ReadAllTextAsync(_bed.CachePath, Ct));
    }

    [TestMethod]
    public async Task Any_other_failure_is_said_with_its_cause()
    {
        _ = _bed.Game("Arena");
        string blocked = _bed.Games.Write("Blocked", "a file where the cache's folder goes");

        CacheSaveResult result = await LibraryCache.SaveAsync(Path.Join(blocked, "library.json"), CacheBed.Key, [await _bed.ReadAsync("Arena", Ct)], _bed.Games.Clock, Ct);

        Assert.IsFalse(result.Saved);
        Assert.IsFalse(result.DriveFull);
        Assert.IsInstanceOfType<IOException>(result.Failure);
        Assert.AreEqual($"the library cache was not saved: {result.Failure.Message}", result.Explain());
    }

    [TestMethod]
    public async Task The_saved_file_holds_the_library_and_no_checks_of_its_own()
    {
        _ = _bed.Game("Arena");
        _ = await _bed.SaveAsync(Ct, await _bed.ReadAsync("Arena", Ct));

        string saved = await File.ReadAllTextAsync(_bed.CachePath, Ct);

        Assert.DoesNotContain("isWellFormed", saved);
    }

    [TestMethod]
    public async Task A_save_that_worked_has_nothing_to_say()
    {
        _ = _bed.Game("Arena");

        CacheSaveResult result = await _bed.SaveAsync(Ct, await _bed.ReadAsync("Arena", Ct));

        Assert.IsTrue(result.Saved);
        Assert.IsNull(result.Explain());
    }

    [TestMethod]
    public async Task A_cancelled_save_still_stops_with_the_cancellation()
    {
        _ = _bed.Game("Arena");
        GameEntry arena = await _bed.ReadAsync("Arena", Ct);
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => LibraryCache.SaveAsync(_bed.CachePath, CacheBed.Key, [arena], _bed.Games.Clock, cancelled.Token));
    }
}
