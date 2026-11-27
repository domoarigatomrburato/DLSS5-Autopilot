using Autopilot.Core.Library;

namespace Autopilot.Core.Tests.Library;

[TestClass]
public sealed class GameNameTests
{
    [TestMethod]
    [DataRow(@"D:\Games\World War Z\bin\WWZ.exe", "World War Z")]
    [DataRow(@"D:\Games\Deus Ex\System\DeusEx.exe", "Deus Ex")]
    [DataRow(@"D:\Games\Ghostwire\Snowfall\Binaries\Win64\GWT.exe", "Snowfall")]
    [DataRow(@"D:\Games\Bayonetta\Bayonetta.exe", "Bayonetta")]
    [DataRow(@"D:\SteamLibrary\steamapps\common\Game\Game.exe", "Game")]
    [DataRow("D:/Games/World War Z/bin/WWZ.exe", "World War Z")]
    public void A_game_added_by_hand_is_named_after_the_nearest_folder_that_says_something(string executable, string expected) =>
        Assert.AreEqual(expected, GameNames.DisplayNameFor(executable));

    [TestMethod]
    [DataRow("bin")]
    [DataRow("bin64")]
    [DataRow("binaries")]
    [DataRow("win64")]
    [DataRow("win32")]
    [DataRow("x64")]
    [DataRow("x86")]
    [DataRow("retail")]
    [DataRow("shipping")]
    [DataRow("release")]
    [DataRow("system")]
    [DataRow("exe")]
    [DataRow("executable")]
    [DataRow("BINARIES")]
    public void Every_generic_folder_name_in_any_case_is_passed_over(string generic) =>
        Assert.AreEqual("Some Game", GameNames.DisplayNameFor($@"D:\Some Game\{generic}\game.exe"));

    [TestMethod]
    [DataRow(@"X:\XboxGames\Forza\Content\gamelaunchhelper.exe")]
    [DataRow(@"X:\xboxgames\Forza\CONTENT\game.exe")]
    [DataRow(@"X:\Games\XboxGames\Forza\Content\game.exe")]
    public void An_xbox_games_content_folder_is_named_after_the_game_above_it(string executable) =>
        Assert.AreEqual("Forza", GameNames.DisplayNameFor(executable));

    [TestMethod]
    [DataRow(@"D:\Games\Forza\Content\game.exe")]
    [DataRow(@"D:\XboxGames\Content\game.exe")]
    public void A_content_folder_elsewhere_is_a_name_of_its_own(string executable) =>
        Assert.AreEqual("Content", GameNames.DisplayNameFor(executable));

    [TestMethod]
    [DataRow(@"C:\bin\x64\game.exe", "x64")]
    [DataRow(@"C:\bin\game.exe", "bin")]
    public void A_path_made_only_of_generic_names_gives_the_executables_own_folder(string executable, string expected) =>
        Assert.AreEqual(expected, GameNames.DisplayNameFor(executable));

    [TestMethod]
    [DataRow("game.exe", "game")]
    [DataRow(@"D:\Tetris.exe", "Tetris")]
    [DataRow("/Tetris.exe", "Tetris")]
    public void An_executable_with_no_folder_is_named_after_itself(string executable, string expected) =>
        Assert.AreEqual(expected, GameNames.DisplayNameFor(executable));
}
