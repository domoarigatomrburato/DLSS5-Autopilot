using Autopilot.Core.Platform;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Platform;

/// <summary>A game is its install folder, normalised: one folder however it is spelled is one game, with one state key.</summary>
[TestClass]
public sealed class GameIdentityTests
{
    [TestMethod]
    public void One_folder_spelled_five_ways_is_one_game_with_one_key()
    {
        using GameTree games = new GameTree("Games").Folder("Some Game");
        string folder = games.PathOf("Some Game");
        string link = games.PathOf("Link");
        _ = Directory.CreateSymbolicLink(link, folder);
        string mixed = folder.Replace(Path.DirectorySeparatorChar, '\\');
        mixed = mixed[..(mixed.Length / 2)] + mixed[(mixed.Length / 2)..].Replace('\\', '/');

        GameIdentity[] spellings =
        [
            GameIdentity.Of(folder),
            GameIdentity.Of(link),
            GameIdentity.Of(folder + Path.DirectorySeparatorChar),
            GameIdentity.Of(games.PathOf("SOME GAME")),
            GameIdentity.Of(mixed),
        ];

        Assert.HasCount(1, spellings.Select(game => game.Key).Distinct(StringComparer.Ordinal));
        Assert.HasCount(1, spellings.Distinct());
        Assert.AreEqual(GameIdentity.Of(folder), GameIdentity.Of(link));
    }

    [TestMethod]
    public void The_folder_is_the_final_path_without_a_trailing_separator()
    {
        using GameTree games = new GameTree("Games").Folder("Game");
        _ = Directory.CreateSymbolicLink(games.PathOf("Link"), games.PathOf("Game"));

        var game = GameIdentity.Of(games.PathOf("Link") + Path.DirectorySeparatorChar);

        Assert.AreEqual(ResolvedPath.Of(games.PathOf("Game")), game.Folder);
        Assert.IsFalse(Path.EndsInDirectorySeparator(game.Folder));
    }

    [TestMethod]
    public void The_key_is_the_first_32_hex_digits_of_the_sha256_of_the_folder_in_windows_form_upper_cased()
    {
        // The SHA-256 of the UTF-8 of "Q:\GAMES\SOME GAME" on Windows, of "\GAMES\SOME GAME" elsewhere.
        (string folder, string key) = OperatingSystem.IsWindows()
            ? (@"Q:\Games\Some Game", "53B859B63F22396292C81C8BF21D4CFC")
            : ("/Games/Some Game", "1712583E4453DE30D75933EF1B755A08");

        Assert.AreEqual(key, GameIdentity.Of(folder).Key);
    }

    [TestMethod]
    public void Different_folders_are_different_games()
    {
        using GameTree games = new("Games");

        var one = GameIdentity.Of(games.PathOf("Foo"));
        var other = GameIdentity.Of(games.PathOf("Foo-other"));

        Assert.AreNotEqual(one, other);
        Assert.AreNotEqual(one.Key, other.Key);
    }
}
