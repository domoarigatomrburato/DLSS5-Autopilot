using Autopilot.Core.Library;

namespace Autopilot.Core.Tests.Library;

[TestClass]
public sealed class CandidateWalkTests(TestContext testContext) : LibraryTestBase(testContext)
{
    private static readonly string[] _skippedNames =
    [
        "_commonredist", "commonredist", "redist", "redistributable", "redistributables", "directx", "dotnet",
        "vcredist", "vc_redist", "easyanticheat", "easyanticheat_eos", "battleye", "punkbuster", "installers",
        "installer", "prerequisites", "prereq", "support", "docs", "manual", "soundtrack", "artbook", "extras",
        "dxsetup", "crashreportclient", "epicwebhelper", "thirdparty", "steamvr", "openvr", "__installer",
        "dotnetfx", "movies", "content", "data", "assets", "textures", "host64",
    ];

    [TestMethod]
    public async Task Folders_five_levels_down_are_searched_and_the_sixth_is_not()
    {
        using GameFolder game = new();
        _ = game.Exe("d1/d2/d3/d4/d5/alpha.exe").Exe("d1/d2/d3/d4/d5/d6/bravo.exe");

        CandidateSearch search = await game.SearchAsync(Ct);

        AssertOrder(game.Relative(search), "d1/d2/d3/d4/d5/alpha.exe");
        Assert.AreEqual(6, search.FoldersSearched);
        Assert.IsFalse(search.StoppedEarly);
    }

    [TestMethod]
    public async Task Folders_listed_are_counted_and_skipped_ones_are_not()
    {
        using GameFolder game = new();
        _ = game.Folder("a/b").Folder("Redist").Folder(".git");

        CandidateSearch search = await game.SearchAsync(Ct);

        Assert.AreEqual(3, search.FoldersSearched);
        Assert.IsEmpty(search.Candidates);
    }

    [TestMethod]
    public async Task Folders_the_game_does_not_live_in_are_skipped_in_any_case_and_their_neighbours_are_not()
    {
        using GameFolder game = new();
        foreach (string name in _skippedNames)
        {
            _ = game.Exe($"{name.ToUpperInvariant()}/{name}.exe");
        }

        _ = game.Exe(".hidden/hidden.exe").Exe("Support Files/kept.exe").Exe("alpha.exe");

        AssertOrder(await game.RankedAsync(Ct), "alpha.exe", "Support Files/kept.exe");
    }

    [TestMethod]
    public async Task An_exe_is_found_by_its_extension_in_any_case_and_other_files_are_not()
    {
        using GameFolder game = new();
        _ = game.Exe("alpha.EXE").Exe("bravo.exe.txt").Exe("charlie.dll");

        AssertOrder(await game.RankedAsync(Ct), "alpha.EXE");
    }

    [TestMethod]
    public async Task The_401st_executable_is_not_seen_and_the_search_says_it_stopped()
    {
        using GameFolder game = new();
        AddExecutables(game, "", 401);

        CandidateSearch search = await game.SearchAsync(Ct);

        Assert.HasCount(400, search.Candidates);
        Assert.DoesNotContain(game.PathOf("e400.exe"), search.Candidates);
        Assert.IsTrue(search.StoppedEarly);
    }

    [TestMethod]
    public async Task Exactly_400_executables_are_all_seen_without_stopping()
    {
        using GameFolder game = new();
        AddExecutables(game, "", 400);

        CandidateSearch search = await game.SearchAsync(Ct);

        Assert.HasCount(400, search.Candidates);
        Assert.IsFalse(search.StoppedEarly);
    }

    [TestMethod]
    public async Task A_shallow_executable_is_seen_before_a_deep_folder_that_comes_first_by_name_fills_the_cap()
    {
        using GameFolder game = new();
        AddExecutables(game, "a/deep", 400);
        _ = game.Exe("b/shallow.exe");

        CandidateSearch search = await game.SearchAsync(Ct);

        Assert.HasCount(400, search.Candidates);
        Assert.Contains(game.PathOf("b/shallow.exe"), search.Candidates);
        Assert.IsTrue(search.StoppedEarly);
    }

    [TestMethod]
    public async Task The_6001st_folder_is_not_seen_and_the_search_says_it_stopped()
    {
        using GameFolder game = new();
        AddFolders(game, 6000);
        _ = game.Exe("d5999/alpha.exe");

        CandidateSearch search = await game.SearchAsync(Ct);

        Assert.IsEmpty(search.Candidates);
        Assert.AreEqual(6000, search.FoldersSearched);
        Assert.IsTrue(search.StoppedEarly);
    }

    [TestMethod]
    public async Task Exactly_6000_folders_are_all_listed_without_stopping()
    {
        using GameFolder game = new();
        AddFolders(game, 5999);
        _ = game.Exe("d5998/alpha.exe");

        CandidateSearch search = await game.SearchAsync(Ct);

        AssertOrder(game.Relative(search), "d5998/alpha.exe");
        Assert.AreEqual(6000, search.FoldersSearched);
        Assert.IsFalse(search.StoppedEarly);
    }

    [TestMethod]
    public async Task A_symbolic_link_to_a_folder_is_not_followed()
    {
        using GameFolder game = new();
        string outside = Path.Join(Path.GetDirectoryName(game.Root), "elsewhere");
        _ = Directory.CreateDirectory(outside);
        File.WriteAllBytes(Path.Join(outside, "linked.exe"), []);
        _ = game.Exe("alpha.exe");
        CreateLinkOrSkip(game.PathOf("link"), outside);

        AssertOrder(await game.RankedAsync(Ct), "alpha.exe");
    }

    [TestMethod]
    public async Task A_folder_that_cannot_be_listed_is_skipped_and_the_rest_is_searched()
    {
        using GameFolder game = new();
        _ = game.Exe("alpha.exe").Exe("locked/bravo.exe").Exe("open/charlie.exe").Deny("locked");

        AssertOrder(await game.RankedAsync(Ct), "alpha.exe", "open/charlie.exe");
    }

    [TestMethod]
    public async Task A_folder_that_does_not_exist_has_no_candidates()
    {
        string missing = Path.Join(Path.GetTempPath(), "autopilot-games-missing", "nowhere");

        CandidateSearch search = await ExecutableCandidates.RankAsync(missing, Ct);

        Assert.IsEmpty(search.Candidates);
        Assert.IsFalse(search.StoppedEarly);
    }

    [TestMethod]
    public async Task A_cancelled_search_throws()
    {
        using GameFolder game = new();
        _ = game.Exe("alpha.exe");
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        _ = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => game.SearchAsync(cancelled.Token));
    }

    private static void AddExecutables(GameFolder game, string folder, int count)
    {
        _ = game.Folder(folder);
        for (int i = 0; i < count; i++)
        {
            File.WriteAllBytes(game.PathOf(Path.Join(folder, $"e{i:D3}.exe")), []);
        }
    }

    private static void AddFolders(GameFolder game, int count)
    {
        for (int i = 0; i < count; i++)
        {
            _ = game.Folder($"d{i:D4}");
        }
    }

    private static void CreateLinkOrSkip(string link, string target)
    {
        try
        {
            _ = Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            Assert.Inconclusive($"cannot create a symbolic link here: {e.Message}");
        }
    }
}
