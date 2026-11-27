using Autopilot.Core.Graphics;
using Autopilot.Core.Library;
using Autopilot.Core.Library.Stores;

namespace Autopilot.Core.Tests.Library.Cache;

/// <summary>A saved game comes back as a scan would list it today: the person's choices, its emulator, one entry per executable.</summary>
[TestClass]
public sealed class CacheChoiceTests(TestContext testContext) : IDisposable
{
    private readonly CacheBed _bed = new();

    private CancellationToken Ct => testContext.CancellationToken;

    public void Dispose() => _bed.Dispose();

    [TestMethod]
    public async Task An_api_chosen_since_the_save_wins_on_load()
    {
        await SaveArenaAsync();
        Choose(GraphicsApi.DX9);

        GameEntry arena = (await _bed.LoadedAsync(Ct)).Games.Single();

        Assert.AreEqual(GraphicsApi.DX9, arena.Api);
        Assert.IsTrue(arena.ApiSetByHand);
        Assert.AreEqual("set by hand (detected DX11)", arena.ApiExplanation);
    }

    [TestMethod]
    public async Task An_api_choice_dropped_since_the_save_gives_back_the_detected_one()
    {
        Choose(GraphicsApi.DX9);
        await SaveArenaAsync();
        Choose(null);

        GameEntry arena = (await _bed.LoadedAsync(Ct)).Games.Single();

        Assert.AreEqual(GraphicsApi.DX11, arena.Api);
        Assert.IsFalse(arena.ApiSetByHand);
        Assert.AreEqual("imports d3d11.dll statically", arena.ApiExplanation);
    }

    [TestMethod]
    public async Task An_emulator_gets_its_profile_back_and_draws_with_what_it_really_uses()
    {
        string eden = _bed.Games.Exe("Emulators/Eden/eden.exe", imports: "d3d12.dll");
        _bed.Age("Emulators/Eden/eden.exe", "Emulators/Eden");
        GameEntry saved = await _bed.ReadAsync("Emulators/Eden", Ct, GameSource.Emulator, "Emulators/Eden/eden.exe");
        _ = await _bed.SaveAsync(Ct, saved);
        Choose(null, "Emulators/Eden");

        GameEntry loaded = (await _bed.LoadedAsync(Ct)).Games.Single();

        Assert.AreEqual(eden, loaded.Executable);
        Assert.AreEqual("yuzu / suyu / Eden / Citron", loaded.Emulator?.Name);
        Assert.AreEqual(GraphicsApi.Vulkan, loaded.Api);
        Assert.AreEqual(GraphicsApi.DX12, loaded.Detected?.Api);
    }

    [TestMethod]
    public async Task Two_saved_entries_with_one_executable_come_back_as_one()
    {
        _ = _bed.Game("Library/GTAIV", "GTAIV.exe");
        _bed.Age("Library/GTAIV/GTAIV.exe", "Library/GTAIV", "Library");
        GameEntry steam = await _bed.ReadAsync("Library", Ct);
        GameEntry rockstar = await _bed.ReadAsync("Library/GTAIV", Ct, GameSource.Rockstar);
        Assert.AreEqual(steam.Executable, rockstar.Executable);
        _ = await _bed.SaveAsync(Ct, rockstar, steam);

        CachedLibrary loaded = await _bed.LoadedAsync(Ct);

        Assert.AreEqual(GameSource.Steam, loaded.Games.Single().Source);
    }

    private async Task SaveArenaAsync()
    {
        _ = _bed.Game("Arena");
        _bed.Age("Arena/Game.exe", "Arena");
        _ = await _bed.SaveAsync(Ct, await _bed.ReadAsync("Arena", Ct));
    }

    private void Choose(GraphicsApi? api, string folder = "Arena") =>
        _bed.Games.Choices.Set(_bed.Games.PathOf(folder), new GameChoices(api, null, null));
}
