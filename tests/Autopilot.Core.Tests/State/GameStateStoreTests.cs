using System.Text.Json.Nodes;
using Autopilot.Core.Platform;
using Autopilot.Core.State;

namespace Autopilot.Core.Tests.State;

/// <summary>What the tool learned about a game is kept per game, written whole by one writer at a time, and never lost to a bad read.</summary>
[TestClass]
public sealed class GameStateStoreTests(TestContext testContext)
{
    private CancellationToken Cancellation => testContext.CancellationToken;

    [TestMethod]
    public void A_game_the_store_has_never_seen_reads_as_empty_defaults()
    {
        using StateBed bed = new();

        Assert.AreEqual(0, bed.Get("attempts"));
        Assert.IsNull(bed.Store.Read(bed.Game).Get("last_verdicts", StateJson.Default.String));
        Assert.IsEmpty(bed.Store.Folders());
        Assert.IsEmpty(bed.Notices());
        Assert.IsFalse(File.Exists(bed.File), "reading writes nothing");
    }

    [TestMethod]
    public void A_change_is_written_to_the_games_own_file_naming_its_schema_and_folder()
    {
        using StateBed bed = new();

        bed.Set("attempts", 3, Cancellation);

        JsonObject saved = bed.OnDisk();
        Assert.AreEqual(1, (int)saved["schema"]!);
        Assert.AreEqual(bed.Game.Folder, (string)saved["folder"]!);
        Assert.AreEqual(3, (int)saved["attempts"]!);
        Assert.AreEqual(3, bed.NewStore().Read(bed.Game).Get("attempts", StateJson.Default.Int32), "another process reads it");
        CollectionAssert.AreEqual(new[] { bed.Game.Folder }, bed.NewStore().Folders().ToArray());
    }

    [TestMethod]
    public void Keys_this_version_does_not_know_are_kept_on_write()
    {
        using StateBed bed = new();
        bed.Plant(/*lang=json,strict*/ """{"schema": 1, "folder": "x", "aim_for": {"preset": "K", "history": [1, 2]}}""");

        bed.Set("attempts", 1, Cancellation);

        Assert.AreEqual(/*lang=json,strict*/ """{"preset":"K","history":[1,2]}""", bed.OnDisk()["aim_for"]!.ToJsonString());
        Assert.AreEqual(bed.Game.Folder, (string)bed.OnDisk()["folder"]!, "the store writes the folder itself");
    }

    [TestMethod]
    public void A_value_of_the_wrong_type_reads_as_its_default_and_the_rest_still_reads()
    {
        using StateBed bed = new();
        bed.Plant(/*lang=json,strict*/ """{"schema": 1, "hotkey": "F10", "plan": 7, "routes": ["native", 3], "name": "ok"}""");
        GameState state = bed.Store.Read(bed.Game);

        Assert.AreEqual(0, state.Get("hotkey", StateJson.Default.Int32));
        Assert.IsNull(state.Get("plan", StateJson.Default.String));
        Assert.IsNull(state.Get("routes", StateJson.Default.StringArray));
        Assert.IsNull(state.Get("nothing", StateJson.Default.String));
        Assert.AreEqual("ok", state.Get("name", StateJson.Default.String));
        Assert.IsEmpty(bed.Notices());
    }

    [TestMethod]
    public void The_keys_the_store_writes_itself_cannot_be_set_and_others_can_be_removed()
    {
        using StateBed bed = new();
        bed.Set("attempts", 2, Cancellation);

        _ = Assert.ThrowsExactly<ArgumentException>(() => bed.Store.Update(bed.Game, state => state.Set("schema", 9, StateJson.Default.Int32), Cancellation));
        _ = Assert.ThrowsExactly<ArgumentException>(() => bed.Store.Update(bed.Game, state => state.Remove("folder"), Cancellation));
        bed.Store.Update(bed.Game, state => state.Remove("attempts"), Cancellation);

        Assert.IsFalse(bed.OnDisk().ContainsKey("attempts"));
        Assert.AreEqual(1, (int)bed.OnDisk()["schema"]!);
    }

    [TestMethod]
    public async Task Two_stores_writing_different_keys_of_one_game_at_once_both_survive()
    {
        using StateBed bed = new();
        GameStateStore other = bed.NewStore();
        const int Writes = 5;

        await Task.WhenAll(
            Task.Run(() => Count(bed.Store, bed.Game, "app", Writes, Cancellation), Cancellation),
            Task.Run(() => Count(other, bed.Game, "cli", Writes, Cancellation), Cancellation));

        Assert.AreEqual(Writes, (int)bed.OnDisk()["app"]!);
        Assert.AreEqual(Writes, (int)bed.OnDisk()["cli"]!);
    }

    [TestMethod]
    public async Task A_writer_waits_for_the_one_holding_the_file_and_its_token_stops_the_wait()
    {
        using StateBed bed = new();
        using CancellationTokenSource impatient = new();
        using var held = HeldMutex.Take(bed.Game);

        Task<bool> writer = Task.Run(() =>
        {
            bed.Set("attempts", 1, impatient.Token);
            return true;
        }, Cancellation);
        impatient.CancelAfter(TimeSpan.FromMilliseconds(250));

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => writer);
        Assert.IsFalse(File.Exists(bed.File));
    }

    [TestMethod]
    public void A_write_cancelled_on_its_way_is_not_made_now_or_later()
    {
        using StateBed bed = new();
        using CancellationTokenSource job = new();
        bed.DuringWrite = job.Cancel;

        _ = Assert.Throws<OperationCanceledException>(() => bed.Set("attempts", 1, job.Token));
        bed.DuringWrite = null;
        bed.Set("fixes", 1, Cancellation);

        Assert.IsFalse(bed.OnDisk().ContainsKey("attempts"));
        Assert.AreEqual(0, bed.Get("attempts"));
        _ = Assert.Throws<OperationCanceledException>(() => bed.Set("attempts", 1, job.Token), "a cancelled token stops before the wait");
    }

    [TestMethod]
    public void A_mutex_its_owner_left_without_releasing_is_taken_over()
    {
        using StateBed bed = new();
        Thread crashed = new(() =>
        {
            using Mutex mutex = new(false, MutexName(bed.Game));
            _ = mutex.WaitOne();
        });
        crashed.Start();
        crashed.Join();

        bed.Set("attempts", 1, Cancellation);

        Assert.AreEqual(1, (int)bed.OnDisk()["attempts"]!);
    }

    [TestMethod]
    public void Dropping_a_game_deletes_its_file_and_forgets_it()
    {
        using StateBed bed = new();
        bed.Set("attempts", 1, Cancellation);
        string other = Path.Join(Path.GetDirectoryName(bed.Game.Folder), "Other Game");
        bed.Store.Update(GameIdentity.Of(other), state => state.Set("attempts", 9, StateJson.Default.Int32), Cancellation);

        bed.Store.Drop(bed.Game, Cancellation);
        bed.Store.Drop(bed.Game, Cancellation);

        Assert.IsFalse(File.Exists(bed.File));
        Assert.AreEqual(0, bed.Get("attempts"));
        CollectionAssert.AreEqual(new[] { GameIdentity.Of(other).Folder }, bed.Store.Folders().ToArray());
    }

    [TestMethod]
    public void The_listing_skips_files_that_name_no_folder()
    {
        using StateBed bed = new();
        bed.Set("attempts", 1, Cancellation);
        string games = Path.GetDirectoryName(bed.File)!;
        TestFiles.Plant(Path.Join(games, "A.json"), /*lang=json,strict*/ """{"schema": 1}""");
        TestFiles.Plant(Path.Join(games, "B.json"), /*lang=json,strict*/ """{"schema": 1, "folder": 5}""");
        TestFiles.Plant(Path.Join(games, "C.json"), "[]");
        TestFiles.Plant(Path.Join(games, "D.json.corrupt"), /*lang=json,strict*/ """{"folder": "D:\\Old"}""");

        CollectionAssert.AreEqual(new[] { bed.Game.Folder }, bed.NewStore().Folders().ToArray());
    }

    private static void Count(GameStateStore store, GameIdentity game, string key, int times, CancellationToken cancellation)
    {
        for (int i = 0; i < times; i++)
        {
            store.Update(game, state => state.Set(key, state.Get(key, StateJson.Default.Int32) + 1, StateJson.Default.Int32), cancellation);
        }
    }

    private static string MutexName(GameIdentity game) => @"Local\invidia-claritatis-games-" + game.Key;

    /// <summary>The game's mutex, held by another thread, as another process would.</summary>
    private sealed class HeldMutex : IDisposable
    {
        private readonly ManualResetEventSlim _taken = new();
        private readonly ManualResetEventSlim _release = new();
        private readonly Thread _owner;

        private HeldMutex(GameIdentity game)
        {
            _owner = new(() =>
            {
                using Mutex mutex = new(false, MutexName(game));
                _ = mutex.WaitOne();
                _taken.Set();
                _release.Wait();
                mutex.ReleaseMutex();
            });
            _owner.Start();
            _taken.Wait();
        }

        public static HeldMutex Take(GameIdentity game) => new(game);

        public void Dispose()
        {
            _release.Set();
            _owner.Join();
            _taken.Dispose();
            _release.Dispose();
        }
    }
}
