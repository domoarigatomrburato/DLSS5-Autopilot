using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;
using Autopilot.Core.Library;

namespace Autopilot.Core.Tests.Library.Cache;

/// <summary>Which saved games are read again on load: what changed on disk, and what can change unseen.</summary>
[TestClass]
public sealed class CacheChangeTests(TestContext testContext) : IDisposable
{
    private readonly CacheBed _bed = new();

    private CancellationToken Ct => testContext.CancellationToken;

    public void Dispose() => _bed.Dispose();

    [TestMethod]
    public async Task A_game_whose_executable_an_update_replaced_is_read_again()
    {
        GameEntry saved = await SavedArenaAsync();
        _ = _bed.Games.Exe("Arena/Game.exe", megabytes: 2, imports: "d3d12.dll");
        _bed.Age("Arena");

        CachedLibrary loaded = await _bed.LoadedAsync(Ct);

        GameEntry arena = loaded.Games.Single();
        Assert.AreEqual(GraphicsApi.DX12, arena.Api, "the update's renderer, not the saved one");
        Assert.AreEqual(GraphicsApi.DX11, saved.Api);
        Assert.AreSame(arena, loaded.ReadAgain.Single());
    }

    [TestMethod]
    public async Task An_executable_of_the_same_size_with_a_new_time_is_read_again()
    {
        _ = await SavedArenaAsync();
        File.SetLastWriteTimeUtc(_bed.Games.PathOf("Arena/Game.exe"), DateTime.UtcNow);

        CachedLibrary loaded = await _bed.LoadedAsync(Ct);

        Assert.HasCount(1, loaded.ReadAgain);
    }

    [TestMethod]
    public async Task An_executable_of_another_size_with_the_old_time_is_read_again()
    {
        _ = await SavedArenaAsync();
        _ = _bed.Game("Arena", megabytes: 3);
        _bed.Age("Arena/Game.exe", "Arena");

        CachedLibrary loaded = await _bed.LoadedAsync(Ct);

        Assert.HasCount(1, loaded.ReadAgain);
    }

    [TestMethod]
    public async Task A_file_dropped_beside_an_executable_below_the_game_folder_reads_the_game_again()
    {
        _ = _bed.Game("Arena/Bin/Win64");
        _bed.Age("Arena/Bin/Win64/Game.exe", "Arena/Bin/Win64", "Arena/Bin", "Arena");
        _ = await _bed.SaveAsync(Ct, await _bed.ReadAsync("Arena", Ct));

        _ = _bed.Games.Write("Arena/Bin/Win64/dxgi.dll", "a mod");

        CachedLibrary loaded = await _bed.LoadedAsync(Ct);

        Assert.HasCount(1, loaded.ReadAgain);
    }

    [TestMethod]
    public async Task A_changed_install_folder_reads_the_game_again()
    {
        _ = _bed.Game("Arena");
        string installed = _bed.Games.Folder("Mods/Arena");
        _bed.Age("Arena/Game.exe", "Arena", "Mods/Arena");
        _ = await _bed.SaveAsync(Ct, await _bed.ReadAsync("Arena", Ct) with { InstallFolder = installed });

        _ = _bed.Games.Write("Mods/Arena/dxgi.dll", "ours");

        CachedLibrary loaded = await _bed.LoadedAsync(Ct);

        Assert.HasCount(1, loaded.ReadAgain);
    }

    [TestMethod]
    public async Task A_game_nothing_touched_comes_back_without_being_read()
    {
        GameEntry saved = await SavedArenaAsync();

        CachedLibrary loaded = await _bed.LoadedAsync(Ct);

        Assert.IsEmpty(loaded.ReadAgain);
        CacheBed.AssertSame(saved, loaded.Games.Single());
    }

    [TestMethod]
    public async Task A_protected_executable_is_read_again_on_every_load_and_takes_the_architecture_chosen_since()
    {
        _ = _bed.Game("Arena");
        _bed.Games.Refuse("Arena/Game.exe");
        _bed.Age("Arena");
        _ = await _bed.SaveAsync(Ct, await _bed.ReadAsync("Arena", Ct));
        _bed.Games.Choices.Set(_bed.Games.PathOf("Arena"), new GameChoices(GraphicsApi.DX11, Bitness.Bits32, null));

        CachedLibrary loaded = await _bed.LoadedAsync(Ct);

        GameEntry arena = loaded.ReadAgain.Single();
        Assert.IsTrue(arena.Protected);
        Assert.AreEqual(Bitness.Bits32, arena.Bitness);
        Assert.AreEqual(GraphicsApi.DX11, arena.Api);
    }

    [TestMethod]
    public async Task A_game_whose_folder_is_gone_is_dropped()
    {
        _ = _bed.Game("Arena");
        _ = _bed.Game("Racer");
        _ = await _bed.SaveAsync(Ct, await _bed.ReadAsync("Arena", Ct), await _bed.ReadAsync("Racer", Ct));

        Directory.Delete(_bed.Games.PathOf("Racer"), recursive: true);

        CachedLibrary loaded = await _bed.LoadedAsync(Ct);

        Assert.AreEqual("Arena", loaded.Games.Single().Name);
    }

    [TestMethod]
    public async Task A_library_whose_every_folder_is_gone_is_no_cache()
    {
        _ = await SavedArenaAsync();

        Directory.Delete(_bed.Games.PathOf("Arena"), recursive: true);

        Assert.IsNull(await _bed.LoadAsync(Ct));
    }

    [TestMethod]
    public async Task A_folder_added_by_hand_that_lost_its_executable_is_dropped_as_a_scan_drops_it()
    {
        _ = _bed.Game("Arena");
        _ = _bed.Game("Mine/Tool", "Tool.exe");
        _bed.Age("Mine/Tool/Tool.exe", "Mine/Tool");
        GameEntry added = ((GameAdded)await _bed.Games.AddAsync(_bed.Games.PathOf("Mine/Tool"), Ct)).Entry;
        _ = await _bed.SaveAsync(Ct, await _bed.ReadAsync("Arena", Ct), added);

        File.Delete(_bed.Games.PathOf("Mine/Tool/Tool.exe"));

        CachedLibrary loaded = await _bed.LoadedAsync(Ct);

        Assert.AreEqual("Arena", loaded.Games.Single().Name);
        Assert.IsEmpty(loaded.ReadAgain);
    }

    [TestMethod]
    public async Task A_folder_added_by_hand_is_read_again_as_a_scan_reads_it()
    {
        _ = _bed.Game("Mine/Tool", "Tool.exe");
        _bed.Age("Mine/Tool/Tool.exe", "Mine/Tool");
        GameEntry added = ((GameAdded)await _bed.Games.AddAsync(_bed.Games.PathOf("Mine/Tool"), Ct)).Entry;
        _ = await _bed.SaveAsync(Ct, added);

        _ = _bed.Games.Exe("Mine/Tool/Tool.exe", megabytes: 2, imports: "d3d12.dll");

        CachedLibrary loaded = await _bed.LoadedAsync(Ct);

        GameEntry tool = loaded.ReadAgain.Single();
        Assert.AreEqual(added.Name, tool.Name);
        Assert.AreEqual(GraphicsApi.DX12, tool.Api);
    }

    [TestMethod]
    public async Task A_game_read_again_slowly_says_so()
    {
        _ = await SavedArenaAsync();
        _ = _bed.Game("Arena", megabytes: 2);
        _bed.Games.Choices.Slow(_bed.Games.PathOf("Arena"), TimeSpan.FromSeconds(3));

        CachedLibrary loaded = await _bed.LoadedAsync(Ct);

        SlowRead slow = loaded.Notes.OfType<SlowRead>().Single();
        Assert.AreEqual("Arena", slow.Name);
        Assert.AreEqual(TimeSpan.FromSeconds(3), slow.Duration);
    }

    private async Task<GameEntry> SavedArenaAsync()
    {
        _ = _bed.Game("Arena");
        _bed.Age("Arena/Game.exe", "Arena");
        GameEntry arena = await _bed.ReadAsync("Arena", Ct);
        Assert.IsTrue((await _bed.SaveAsync(Ct, arena)).Saved);
        return arena;
    }
}
