using System.Globalization;
using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;
using Autopilot.Core.Library;
using Autopilot.Core.Library.Stores;

namespace Autopilot.Core.Tests.Library.Scanning;

/// <summary>A full scan: every source in order, each game read once, one entry per game, sorted.</summary>
[TestClass]
public sealed class ScanTests(TestContext testContext) : IDisposable
{
    private readonly ScanBed _bed = new();

    private CancellationToken Ct => testContext.CancellationToken;

    public void Dispose() => _bed.Dispose();

    [TestMethod]
    public async Task Sources_are_asked_in_the_stores_order_with_the_emulator_search_last()
    {
        ScanRequest request = _bed.Full(_bed.Lists(GameSource.Folder), _bed.Lists(GameSource.Epic), _bed.Lists(GameSource.Steam))
            with
        { FullScanOnlySources = [_bed.Lists(GameSource.Emulator)] };

        string[] steps = await Scans.ProgressAsync(request, Ct);

        Assert.AreEqual("Scanning Steam..., Scanning Epic..., Scanning Folders..., Scanning Emulators...", string.Join(", ", steps));
    }

    [TestMethod]
    public async Task A_source_that_fails_is_noted_and_the_others_are_still_listed()
    {
        ScanRequest request = _bed.Full(FakeSource.Failing(GameSource.Steam, new InvalidOperationException("broken")), _bed.Lists(GameSource.Epic, "Game"));

        LibraryScan scan = await Scans.ScanAsync(request, Ct);

        Assert.AreEqual("Game (Epic)", Scans.Listed(scan));
        Assert.AreEqual("Steam could not be read: InvalidOperationException", Scans.Note<SourceFailed>(scan.Notes));
    }

    [TestMethod]
    public async Task A_cancelled_scan_stops_and_says_so()
    {
        using CancellationTokenSource cancel = new();
        await cancel.CancelAsync();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => Scans.ScanAsync(_bed.Full(_bed.Lists(GameSource.Steam)), cancel.Token));
    }

    [TestMethod]
    public async Task A_source_cancelled_inside_is_not_taken_for_a_failure()
    {
        ScanRequest request = _bed.Full(FakeSource.Failing(GameSource.Steam, new OperationCanceledException()));

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => Scans.ScanAsync(request, Ct));
    }

    [TestMethod]
    public async Task A_folder_two_sources_list_is_one_game_the_first_sources()
    {
        _ = _bed.Exe("Games/Game/Game.exe");
        ScanRequest request = _bed.Full(_bed.Lists(GameSource.Folder, "Games/Game"), _bed.Lists(GameSource.Gog, "Games/GAME/"));

        LibraryScan scan = await Scans.ScanAsync(request, Ct);

        Assert.AreEqual(GameSource.Gog, scan.Games.Single().Source);
    }

    [TestMethod]
    public async Task Progress_names_every_game_read_out_of_the_right_total()
    {
        ScanRequest request = _bed.Full(_bed.Lists(GameSource.Steam, "A", "B")) with { HandAddedFolders = [_bed.Folder("Mine")] };

        string[] steps = await Scans.ProgressAsync(request, Ct);

        Assert.AreEqual("Inspecting games... 1/3: A, Inspecting games... 2/3: B, Inspecting games... 3/3: Mine", string.Join(", ", steps.Skip(1)));
    }

    [TestMethod]
    public async Task The_library_is_sorted_by_name_ignoring_case_then_by_folder()
    {
        var steam = FakeSource.Listing(GameSource.Steam, _bed.Game("beta", "1"), _bed.Game("Alpha", "2"), _bed.Game("alpha", "0"));

        LibraryScan scan = await Scans.ScanAsync(_bed.Full(steam), Ct);

        Assert.AreEqual("0, 2, 1", string.Join(", ", scan.Games.Select(game => Path.GetFileName(game.Folder))));
    }

    [TestMethod]
    public async Task A_game_that_takes_a_second_or_more_to_read_is_noted()
    {
        _bed.Choices.Slow(_bed.PathOf("Slow"), TimeSpan.FromMilliseconds(1300));
        _bed.Choices.Slow(_bed.PathOf("Quick"), TimeSpan.FromMilliseconds(900));

        LibraryScan scan = await Scans.ScanAsync(_bed.Full(_bed.Lists(GameSource.Steam, "Slow", "Quick")), Ct);

        Assert.AreEqual($"inspected Slow in {1.3.ToString("0.0", CultureInfo.CurrentCulture)}s", Scans.Note<SlowRead>(scan.Notes));
    }

    [TestMethod]
    public async Task Folders_without_an_executable_are_listed_and_counted()
    {
        _ = _bed.Exe("Game/Game.exe");

        LibraryScan scan = await Scans.ScanAsync(_bed.Full(_bed.Lists(GameSource.Steam, "Game", "Empty", "Bare")), Ct);

        Assert.AreEqual("Bare (Steam), Empty (Steam), Game (Steam)", Scans.Listed(scan));
        Assert.AreEqual("2 folders had no executable", scan.ExplainFoldersWithoutExecutable());
        Assert.HasCount(2, scan.Notes);
    }

    [TestMethod]
    public async Task A_single_folder_without_an_executable_is_counted_in_the_singular()
    {
        LibraryScan scan = await Scans.ScanAsync(_bed.Full(_bed.Lists(GameSource.Steam, "Empty")), Ct);

        Assert.AreEqual("1 folder had no executable", scan.ExplainFoldersWithoutExecutable());
    }

    [TestMethod]
    public async Task A_library_where_every_game_has_an_executable_has_nothing_to_count()
    {
        _ = _bed.Exe("Game/Game.exe");

        LibraryScan scan = await Scans.ScanAsync(_bed.Full(_bed.Lists(GameSource.Steam, "Game")), Ct);

        Assert.IsNull(scan.ExplainFoldersWithoutExecutable());
    }

    [TestMethod]
    public async Task GTA_IV_listed_by_Steam_and_Rockstar_is_one_game_the_first_stores()
    {
        LibraryScan scan = await Scans.ScanAsync(_bed.Full(GtaSources()), Ct);

        Assert.AreEqual("Grand Theft Auto IV (Steam)", Scans.Listed(scan));
    }

    [TestMethod]
    [DataRow(GraphicsApi.DX9, null)]
    [DataRow(null, Bitness.Bits32)]
    public async Task GTA_IV_is_listed_under_the_folder_a_choice_was_made_for(GraphicsApi? api, Bitness? bitness)
    {
        IGameSource[] sources = GtaSources();
        _bed.Choices.Set(_bed.PathOf("Grand Theft Auto IV/GTAIV"), new GameChoices(api, bitness, null));

        LibraryScan scan = await Scans.ScanAsync(_bed.Full(sources), Ct);

        Assert.AreEqual("GTAIV (Rockstar)", Scans.Listed(scan));
    }

    [TestMethod]
    public async Task Games_sharing_an_emulators_executable_are_all_kept()
    {
        _ = _bed.Exe("Emu/emu.exe");
        var emulators = FakeSource.Listing(GameSource.Emulator, _bed.Game("Game A", "Emu", GameSource.Emulator, "Emu/emu.exe"), _bed.Game("Game B", "Emu/roms", GameSource.Emulator, "Emu/emu.exe"));

        LibraryScan scan = await Scans.ScanAsync(_bed.Full() with { FullScanOnlySources = [emulators] }, Ct);

        Assert.HasCount(2, scan.Games);
    }

    // Steam lists the library folder, the Rockstar launcher the GTAIV folder inside it: one executable.
    private IGameSource[] GtaSources()
    {
        _ = _bed.Exe("Grand Theft Auto IV/GTAIV/GTAIV.exe");
        return [_bed.Lists(GameSource.Rockstar, "Grand Theft Auto IV/GTAIV"), _bed.Lists(GameSource.Steam, "Grand Theft Auto IV")];
    }
}
