using Autopilot.Core.Library;
using Autopilot.Core.Library.Stores;

namespace Autopilot.Core.Tests.Library.Scanning;

/// <summary>Folders the person added: read back after every full scan, and added one at a time.</summary>
[TestClass]
public sealed class HandAddedTests(TestContext testContext) : IDisposable
{
    private readonly ScanBed _bed = new();

    private CancellationToken Ct => testContext.CancellationToken;

    public void Dispose() => _bed.Dispose();

    [TestMethod]
    public async Task A_remembered_folder_comes_back_after_a_full_scan_named_after_its_executables_folder()
    {
        string exe = _bed.Exe("Ghostwire/Snowfall/Binaries/Win64/GWT.exe");

        LibraryScan scan = await Scans.ScanAsync(_bed.Full() with { HandAddedFolders = [_bed.PathOf("Ghostwire")] }, Ct);

        Assert.AreEqual("Snowfall (Manual)", Scans.Listed(scan));
        Assert.AreEqual(exe, scan.Games.Single().Executable);
    }

    [TestMethod]
    public async Task A_remembered_folder_that_is_relative_missing_given_twice_already_listed_or_empty_is_skipped_without_a_word()
    {
        _ = _bed.Exe("Listed/Game.exe");
        _ = _bed.Exe("Twice/Game.exe");
        string[] remembered = ["Twice", _bed.PathOf("Missing"), _bed.PathOf("Listed"), _bed.PathOf("Twice"), _bed.PathOf("Twice") + Path.DirectorySeparatorChar, _bed.Folder("Empty")];

        LibraryScan scan = await Scans.ScanAsync(_bed.Full(_bed.Lists(GameSource.Steam, "Listed")) with { HandAddedFolders = remembered }, Ct);

        Assert.AreEqual("Listed (Steam), Twice (Manual)", Scans.Listed(scan));
        Assert.IsEmpty(scan.Notes);
    }

    [TestMethod]
    public async Task A_stores_entry_wins_over_a_remembered_folder_for_the_same_executable()
    {
        _ = _bed.Exe("Lib/Game/Game.exe");

        LibraryScan scan = await Scans.ScanAsync(_bed.Full(_bed.Lists(GameSource.Steam, "Lib")) with { HandAddedFolders = [_bed.PathOf("Lib/Game")] }, Ct);

        Assert.AreEqual("Lib (Steam)", Scans.Listed(scan));
    }

    [TestMethod]
    public async Task Adding_an_executable_adds_it_as_the_only_candidate()
    {
        string exe = _bed.Exe("World War Z/bin/WWZ.exe");
        _ = _bed.Exe("World War Z/bin/Other.exe", megabytes: 300);

        GameAdded added = Assert.IsInstanceOfType<GameAdded>(await _bed.AddAsync(exe, Ct));

        Assert.AreEqual(("World War Z", GameSource.Manual, exe, _bed.PathOf("World War Z/bin")), (added.Entry.Name, added.Entry.Source, added.Entry.Executable, added.Entry.Folder));
        Assert.AreEqual(exe, added.Entry.Candidates.Single());
    }

    [TestMethod]
    public async Task Adding_an_executable_keeps_it_even_when_an_earlier_install_names_another()
    {
        string exe = _bed.Exe("Game/Game.exe");
        _ = _bed.Exe("Game/Launcher.exe");
        _bed.Installed("Game", "Launcher.exe");

        GameAdded added = Assert.IsInstanceOfType<GameAdded>(await _bed.AddAsync(exe, Ct));

        Assert.AreEqual(exe, added.Entry.Executable);
    }

    [TestMethod]
    public async Task Adding_a_folder_ranks_its_executables_and_leaves_out_its_installer()
    {
        _ = _bed.Exe("Deus Ex/System/Setup.exe");
        string exe = _bed.Exe("Deus Ex/System/DeusEx.exe");

        GameAdded added = Assert.IsInstanceOfType<GameAdded>(await _bed.AddAsync(_bed.PathOf("Deus Ex"), Ct));

        Assert.AreEqual(("Deus Ex", exe), (added.Entry.Name, added.Entry.Executable));
        Assert.AreEqual(exe, added.Entry.Candidates.Single());
    }

    [TestMethod]
    public async Task Adding_a_folder_already_in_the_library_opens_that_game()
    {
        _ = _bed.Exe("Game/Game.exe");
        GameEntry listed = new() { Name = "Listed", Folder = _bed.PathOf("Game"), Source = GameSource.Steam };

        AlreadyListed already = Assert.IsInstanceOfType<AlreadyListed>(await _bed.AddAsync(_bed.PathOf("Game") + Path.DirectorySeparatorChar, Ct, listed));

        Assert.AreEqual("Listed is already in the list", already.Explain());
    }

    [TestMethod]
    public async Task Adding_an_executable_another_entry_uses_opens_that_game()
    {
        string exe = _bed.Exe("Lib/GTAIV/GTAIV.exe");
        GameEntry listed = new() { Name = "Listed", Folder = _bed.PathOf("Lib"), Source = GameSource.Steam, Executable = exe };

        AddResult result = await _bed.AddAsync(exe, Ct, listed);

        Assert.AreEqual(new AlreadyListed(listed), result);
    }

    [TestMethod]
    public async Task Adding_a_folder_with_no_executable_adds_nothing()
    {
        string folder = _bed.Folder("Empty");

        NoExecutableToAdd none = Assert.IsInstanceOfType<NoExecutableToAdd>(await _bed.AddAsync(folder, Ct));

        Assert.AreEqual($"No executable was found in: {folder}", none.Explain());
    }

    [TestMethod]
    [DataRow("Missing")]
    [DataRow("Game/readme.txt")]
    [DataRow("relative")]
    public async Task Adding_a_path_that_is_neither_an_executable_nor_a_folder_says_it_was_not_found(string relative)
    {
        _ = _bed.NotAnExe("Game/readme.txt");
        string path = relative == "relative" ? "Game" : _bed.PathOf(relative);

        AddPathNotFound missing = Assert.IsInstanceOfType<AddPathNotFound>(await _bed.AddAsync(path, Ct));

        Assert.AreEqual($"{path} not found.", missing.Explain());
    }
}
