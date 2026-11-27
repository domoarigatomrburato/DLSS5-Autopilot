using Autopilot.Core.Install;
using Autopilot.Core.Library;
using Autopilot.Core.Library.Stores;

namespace Autopilot.Core.Tests.Library.Scanning;

/// <summary>A quick scan: the stores asked again, known games kept as they were, only new folders read.</summary>
[TestClass]
public sealed class QuickScanTests(TestContext testContext) : IDisposable
{
    // A word no reading writes, so a kept entry shows it was not read again.
    private const string ReadBefore = "read by an earlier scan";

    private readonly ScanBed _bed = new();

    private CancellationToken Ct => testContext.CancellationToken;

    public void Dispose() => _bed.Dispose();

    [TestMethod]
    public async Task A_game_a_store_still_lists_is_kept_as_it_was_read()
    {
        LibraryScan scan = await QuickAsync(Known("Game", GameSource.Steam), "Game");

        Assert.AreEqual(ReadBefore, scan.Games.Single().ApiExplanation);
    }

    [TestMethod]
    public async Task A_game_its_store_no_longer_lists_is_dropped_though_its_folder_stays()
    {
        LibraryScan scan = await QuickAsync(Known("Game", GameSource.Steam));

        Assert.IsEmpty(scan.Games);
    }

    [TestMethod]
    [DataRow(GameSource.Manual)]
    [DataRow(GameSource.Emulator)]
    public async Task A_game_no_store_lists_in_the_first_place_is_kept(GameSource source)
    {
        LibraryScan scan = await QuickAsync(Known("Game", source));

        Assert.AreEqual(ReadBefore, scan.Games.Single().ApiExplanation);
    }

    [TestMethod]
    [DataRow(InstallState.Installed)]
    [DataRow(InstallState.Installing)]
    [DataRow(InstallState.Removing)]
    [DataRow(InstallState.Unreadable)]
    public async Task A_game_with_an_install_of_ours_is_kept_so_it_can_be_uninstalled(InstallState state)
    {
        LibraryScan scan = await QuickAsync(Known("Game", GameSource.Steam) with { Install = state });

        Assert.AreEqual(ReadBefore, scan.Games.Single().ApiExplanation);
    }

    [TestMethod]
    [DataRow("Game/bin")]
    [DataRow("Game")]
    [DataRow("Game/Elsewhere")]
    public async Task A_folder_that_holds_a_record_made_since_the_last_read_is_never_dropped(string recorded)
    {
        GameEntry known = Known("Game", GameSource.Steam) with { InstallFolder = _bed.Folder("Game/Elsewhere") };
        _bed.Records.Add(_bed.PathOf(recorded), InstallState.Installed);

        LibraryScan scan = await QuickAsync(known);

        Assert.HasCount(1, scan.Games);
    }

    [TestMethod]
    [DataRow(GameSource.Steam)]
    [DataRow(GameSource.Manual)]
    public async Task A_game_whose_folder_is_gone_is_dropped(GameSource source)
    {
        GameEntry known = Known("Game", source) with { Install = InstallState.Installed };
        Directory.Delete(_bed.PathOf("Game"), recursive: true);

        LibraryScan scan = await QuickAsync(known, "Game");

        Assert.IsEmpty(scan.Games);
    }

    [TestMethod]
    public async Task Only_a_folder_seen_for_the_first_time_is_read()
    {
        _ = _bed.Exe("New/New.exe");

        string[] steps = await Scans.ProgressAsync(_bed.Quick([Known("Game", GameSource.Steam)], _bed.Lists(GameSource.Steam, "Game", "New")), Ct);

        Assert.AreEqual("Scanning Steam..., Inspecting games... 1/1: New", string.Join(", ", steps));
    }

    [TestMethod]
    public async Task The_emulator_search_and_the_folders_added_by_hand_wait_for_a_full_scan()
    {
        var emulators = FakeSource.Listing(GameSource.Emulator);
        _ = _bed.Exe("Mine/Mine.exe");
        ScanRequest request = _bed.Quick([]) with { FullScanOnlySources = [emulators], HandAddedFolders = [_bed.PathOf("Mine")] };

        LibraryScan scan = await Scans.ScanAsync(request, Ct);

        Assert.IsFalse(emulators.Asked);
        Assert.IsEmpty(scan.Games);
    }

    [TestMethod]
    public async Task GTA_IV_installed_through_Rockstars_folder_since_the_last_scan_is_listed_under_that_folder()
    {
        GameEntry steam = Known("GTA", GameSource.Steam) with { Executable = _bed.Exe("GTA/GTAIV/GTAIV.exe") };
        _bed.Records.Add(_bed.PathOf("GTA/GTAIV"), InstallState.Installed, "GTAIV.exe");
        ScanRequest request = _bed.Quick([steam], _bed.Lists(GameSource.Steam, "GTA"), _bed.Lists(GameSource.Rockstar, "GTA/GTAIV"));

        LibraryScan scan = await Scans.ScanAsync(request, Ct);

        Assert.AreEqual("GTAIV (Rockstar)", Scans.Listed(scan));
    }

    // A game read before, with its executable under bin, and its folder still on disk.
    private GameEntry Known(string folder, GameSource source) => new()
    {
        Name = folder,
        Folder = _bed.Folder(folder),
        Source = source,
        Executable = _bed.Exe($"{folder}/bin/Game.exe"),
        ApiExplanation = ReadBefore,
    };

    // A quick scan where Steam now lists these folders.
    private Task<LibraryScan> QuickAsync(GameEntry known, params string[] steamFolders) =>
        Scans.ScanAsync(_bed.Quick([known], _bed.Lists(GameSource.Steam, steamFolders)), Ct);
}
