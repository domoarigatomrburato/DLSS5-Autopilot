using Autopilot.Core.Library.Protection;

namespace Autopilot.Core.Tests.Library.Protection;

[TestClass]
public sealed class ReEngineTests
{
    [TestMethod]
    public void The_marker_in_the_executables_folder_makes_it_an_RE_Engine_game()
    {
        using GameFolder game = new();
        _ = game.Exe("bin/re4.exe").Exe("bin/re_chunk_000.pak");

        Assert.IsTrue(ReEngine.IsReEngine(game.PathOf("bin"), game.Root));
    }

    [TestMethod]
    public void The_marker_in_the_game_folder_is_seen_when_the_executable_is_in_a_subfolder()
    {
        using GameFolder game = new();
        _ = game.Exe("bin/re4.exe").Exe("re_chunk_000.pak");

        Assert.IsTrue(ReEngine.IsReEngine(game.PathOf("bin"), game.Root));
    }

    [TestMethod]
    [DataRow("RE_CHUNK_000.PAK")]
    [DataRow("Re_Chunk_000.Pak")]
    public void The_marker_is_found_in_any_case(string fileName)
    {
        using GameFolder game = new();
        _ = game.Exe(fileName);

        Assert.IsTrue(ReEngine.IsReEngine(game.Root, game.Root));
    }

    [TestMethod]
    public void A_game_without_the_marker_is_not_an_RE_Engine_game()
    {
        using GameFolder game = new();
        _ = game.Exe("Game.exe").Exe("re_chunk_001.pak").Exe("nested/re_chunk_000.pak").Exe("re_chunk_000.pak.bak").Folder("re_chunk_000.pak.d");

        Assert.IsFalse(ReEngine.IsReEngine(game.Root, game.Root));
    }

    [TestMethod]
    public void A_folder_named_like_the_marker_is_not_the_marker_file()
    {
        using GameFolder game = new();
        _ = game.Folder("re_chunk_000.pak");

        Assert.IsFalse(ReEngine.IsReEngine(game.Root, game.Root));
    }

    [TestMethod]
    public void A_folder_that_is_missing_or_cannot_be_read_has_no_marker_and_the_other_is_still_searched()
    {
        using GameFolder game = new();
        _ = game.Exe("locked/re_chunk_000.pak").Exe("re_chunk_000.pak");
        _ = game.Deny("locked");
        string missing = game.PathOf("gone");

        Assert.IsFalse(ReEngine.IsReEngine(missing, missing));
        Assert.IsFalse(ReEngine.IsReEngine(game.PathOf("locked"), missing));
        Assert.IsFalse(ReEngine.IsReEngine(string.Empty, missing));
        Assert.IsTrue(ReEngine.IsReEngine(missing, game.Root));
    }

    [TestMethod]
    public void The_warning_says_which_file_it_rests_on_and_what_the_install_does()
    {
        string text = ReEngine.Warning;

        Assert.StartsWith("This looks like a Capcom RE Engine game (re_chunk_000.pak is in the folder)", text);
        Assert.Contains("This install puts REFramework in first", text);
        Assert.EndsWith("REFramework's own build has to catch up first.", text);
    }
}
