using Autopilot.Core.Executables;
using Autopilot.Core.Install;
using Autopilot.Core.Library;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Tests.Executables;

namespace Autopilot.Core.Tests.Library.Scanning;

/// <summary>Which executable a game is set up against: the person's pick, the store's, the ranking's, an earlier install's.</summary>
[TestClass]
public sealed class ExecutableChoiceTests(TestContext testContext) : IDisposable
{
    private readonly ScanBed _bed = new();

    private CancellationToken Ct => testContext.CancellationToken;

    public void Dispose() => _bed.Dispose();

    [TestMethod]
    public async Task Ghostwire_is_set_up_beside_the_real_executable_not_the_stores_stub_in_the_root()
    {
        _ = _bed.Exe("Ghostwire Tokyo/GWT.exe");
        string real = _bed.Exe("Ghostwire Tokyo/Snowfall/Binaries/Win64/GWT.exe", megabytes: 120);

        GameRead read = await _bed.ReadWithNotesAsync(_bed.Game("Ghostwire: Tokyo", "Ghostwire Tokyo", GameSource.Epic, "Ghostwire Tokyo/GWT.exe"), Ct);

        Assert.AreEqual(real, read.Entry.Executable);
        Assert.AreEqual($"Ghostwire: Tokyo: the store names GWT.exe but the game runs from {Path.Join("Snowfall", "Binaries", "Win64", "GWT.exe")} - using that", Scans.Note<StorePickReplaced>(read.Notes));
    }

    [TestMethod]
    public async Task A_store_pick_stands_when_the_ranking_finds_nothing_under_a_Binaries_folder()
    {
        string store = _bed.Exe("Game/Play.exe");
        _ = _bed.Exe("Game/bin/Bigger.exe", megabytes: 200);

        GameRead read = await _bed.ReadWithNotesAsync(_bed.Game("Game", "Game", GameSource.Epic, "Game/Play.exe"), Ct);

        Assert.AreEqual(store, read.Entry.Executable);
        Assert.IsEmpty(read.Notes);
    }

    [TestMethod]
    public async Task A_store_pick_already_under_a_Binaries_folder_stands()
    {
        string store = _bed.Exe("Game/Binaries/Win64/Small.exe");
        _ = _bed.Exe("Game/Engine/Binaries/Win64/Big-Shipping.exe", megabytes: 200);

        GameEntry entry = await _bed.ReadAsync(_bed.Game("Game", "Game", GameSource.Epic, "Game/Binaries/Win64/Small.exe"), Ct);

        Assert.AreEqual(store, entry.Executable);
    }

    [TestMethod]
    public async Task Kingdom_Come_2_is_set_up_beside_its_executable_deep_under_Bin()
    {
        string exe = _bed.Exe("KingdomComeDeliverance2/Bin/Win64MasterMasterSteamPGO/KingdomCome.exe", megabytes: 90);

        GameEntry entry = await _bed.ReadAsync(_bed.Game("Kingdom Come: Deliverance II", "KingdomComeDeliverance2"), Ct);

        Assert.AreEqual(exe, entry.Executable);
        Assert.AreEqual(exe, entry.Candidates.Single());
        Assert.IsNull(entry.InstallFolder);
    }

    [TestMethod]
    public async Task Conan_Exiles_keeps_the_executable_the_person_picked_over_the_launcher_and_an_earlier_install_beside_it()
    {
        _ = _bed.Exe("Conan Exiles/FuncomLauncher.exe", megabytes: 50);
        string shipping = _bed.Exe("Conan Exiles/ConanSandbox/Binaries/Win64/ConanSandbox.exe", megabytes: 80);
        _bed.Installed("Conan Exiles", "FuncomLauncher.exe");
        _bed.Choices.Set(_bed.PathOf("Conan Exiles"), new GameChoices(null, null, shipping));

        GameRead read = await _bed.ReadWithNotesAsync(_bed.Game("Conan Exiles", "Conan Exiles", GameSource.Steam, "Conan Exiles/FuncomLauncher.exe"), Ct);

        Assert.AreEqual(shipping, read.Entry.Executable);
        Assert.IsNull(read.Entry.InstallFolder);
        Assert.IsNull(read.Entry.Install);
        Assert.IsEmpty(read.Notes);
    }

    [TestMethod]
    public async Task A_picked_executable_that_is_gone_falls_back_to_the_stores_pick()
    {
        string store = _bed.Exe("Game/Game.exe");
        _bed.Choices.Set(_bed.PathOf("Game"), new GameChoices(null, null, _bed.PathOf("Game/Removed.exe")));

        GameEntry entry = await _bed.ReadAsync(_bed.Game("Game", "Game", GameSource.Epic, "Game/Game.exe"), Ct);

        Assert.AreEqual(store, entry.Executable);
    }

    [TestMethod]
    public async Task An_earlier_install_in_another_candidates_folder_is_adopted_with_the_executable_it_names()
    {
        _ = _bed.Exe("Game/Bin/Win64/Game.exe", megabytes: 60);
        string installed = _bed.Exe("Game/Bin/Win32/Game.exe", megabytes: 40, machine: TestPe.I386);
        _bed.Installed("Game/Bin/Win32", "Game.exe");

        GameRead read = await _bed.ReadWithNotesAsync(_bed.Game("Game", "Game"), Ct);

        Assert.AreEqual(installed, read.Entry.Executable);
        Assert.AreEqual(InstallState.Installed, read.Entry.Install);
        Assert.IsNull(read.Entry.InstallFolder);
        Assert.AreEqual(Bitness.Bits32, read.Entry.Bitness);
        Assert.AreEqual($"Game: an earlier install is recorded in {_bed.PathOf("Game/Bin/Win32")}, not next to Game.exe", Scans.Note<InstallRecordedElsewhere>(read.Notes));
    }

    [TestMethod]
    public async Task A_record_naming_a_missing_executable_adopts_a_candidate_in_its_folder()
    {
        _ = _bed.Exe("Game/Bin/Win64/Game.exe", megabytes: 60);
        string other = _bed.Exe("Game/Bin/Win32/Other.exe", megabytes: 40);
        _bed.Installed("Game/Bin/Win32", "Gone.exe");

        GameEntry entry = await _bed.ReadAsync(_bed.Game("Game", "Game"), Ct);

        Assert.AreEqual(other, entry.Executable);
    }

    [TestMethod]
    [DataRow("../Outside.exe")]
    [DataRow(@"..\Outside.exe")]
    public async Task A_record_naming_a_file_outside_its_folder_is_not_followed(string named)
    {
        string exe = _bed.Exe("Game/Game.exe");
        _ = _bed.Exe("Outside.exe");
        _bed.Installed("Game", named);

        GameEntry entry = await _bed.ReadAsync(_bed.Game("Game", "Game"), Ct);

        Assert.AreEqual(exe, entry.Executable);
        Assert.AreEqual(InstallState.Installed, entry.Install);
    }

    [TestMethod]
    public async Task A_record_in_the_game_folder_away_from_the_executable_is_the_install_folder()
    {
        string exe = _bed.Exe("Game/bin/Game.exe");
        _bed.Records.Add(_bed.PathOf("Game"), InstallState.Removing);

        GameEntry entry = await _bed.ReadAsync(_bed.Game("Game", "Game"), Ct);

        Assert.AreEqual(exe, entry.Executable);
        Assert.AreEqual(_bed.PathOf("Game"), entry.InstallFolder);
        Assert.AreEqual(InstallState.Removing, entry.Install);
    }

    [TestMethod]
    public async Task A_record_beside_the_executable_is_its_install_without_a_note()
    {
        _ = _bed.Exe("Game/Game.exe");
        _bed.Records.Add(_bed.PathOf("Game"), InstallState.Unreadable);

        GameRead read = await _bed.ReadWithNotesAsync(_bed.Game("Game", "Game"), Ct);

        Assert.AreEqual(InstallState.Unreadable, read.Entry.Install);
        Assert.IsNull(read.Entry.InstallFolder);
        Assert.IsEmpty(read.Notes);
    }

    [TestMethod]
    public async Task A_folder_too_big_to_search_says_so()
    {
        FillPastTheCap(_bed.Folder("Huge"));

        GameRead read = await _bed.ReadWithNotesAsync(_bed.Game("Huge", "Huge"), Ct);

        Assert.AreEqual($"stopped looking for executables under {_bed.PathOf("Huge")} after 1 folders - too big to search", Scans.Note<SearchStoppedEarly>(read.Notes));
    }

    // One executable more than the search looks at.
    private static void FillPastTheCap(string folder)
    {
        for (int i = 0; i < 401; i++)
        {
            File.WriteAllBytes(Path.Join(folder, $"e{i:D3}.exe"), []);
        }
    }
}
