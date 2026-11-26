using Autopilot.Core.Library.Protection;

namespace Autopilot.Core.Tests.Library.Protection;

[TestClass]
public sealed class AntiCheatFailureTests(TestContext testContext) : ProtectionTestBase(testContext)
{
    [TestMethod]
    public async Task A_subfolder_that_cannot_be_read_does_not_hide_an_anti_cheat_found_elsewhere()
    {
        using GameFolder game = new();
        _ = game.Exe("faceit.exe").Exe("Locked/beservice.dll").Exe("Open/readme.txt");
        _ = game.Deny("Locked");

        AntiCheatFinding finding = await AntiCheat.DetectAsync(game.Root, game.Root, Ct);

        Assert.AreEqual(AntiCheatVerdict.Present, finding.Verdict);
        AssertNames(finding.Products, "FACEIT AC");
        AssertNames(finding.Evidence, "faceit.exe");
    }

    [TestMethod]
    public async Task A_game_folder_that_cannot_be_read_gives_no_answer()
    {
        using GameFolder game = new();
        _ = game.Exe("battleye.dll");
        _ = game.Deny(string.Empty);

        AntiCheatFinding finding = await AntiCheat.DetectAsync(game.Root, game.Root, Ct);

        Assert.AreEqual(AntiCheatVerdict.Unknown, finding.Verdict);
        Assert.IsEmpty(finding.Products);
    }

    [TestMethod]
    public async Task A_folder_that_does_not_exist_gives_no_answer_even_when_the_other_is_clean()
    {
        using GameFolder game = new();
        _ = game.Exe("Game.exe");
        string missing = game.PathOf("gone");

        Assert.AreEqual(AntiCheatVerdict.Unknown, (await AntiCheat.DetectAsync(missing, game.Root, Ct)).Verdict);
        Assert.AreEqual(AntiCheatVerdict.Unknown, (await AntiCheat.DetectAsync(game.Root, missing, Ct)).Verdict);
        Assert.AreEqual(AntiCheatVerdict.Unknown, (await AntiCheat.DetectAsync(missing, missing, Ct)).Verdict);
    }

    [TestMethod]
    public async Task A_path_that_is_a_file_or_empty_gives_no_answer()
    {
        using GameFolder game = new();
        _ = game.Exe("Game.exe");

        Assert.AreEqual(AntiCheatVerdict.Unknown, (await AntiCheat.DetectAsync(game.PathOf("Game.exe"), game.Root, Ct)).Verdict);
        Assert.AreEqual(AntiCheatVerdict.Unknown, (await AntiCheat.DetectAsync(game.Root, string.Empty, Ct)).Verdict);
    }

    [TestMethod]
    public async Task A_clean_folder_gives_none()
    {
        using GameFolder game = new();
        _ = game.Exe("Game.exe").Exe("Data/level1.pak");

        AntiCheatFinding finding = await AntiCheat.DetectAsync(game.Root, game.Root, Ct);

        Assert.AreEqual(AntiCheatVerdict.None, finding.Verdict);
    }

    [TestMethod]
    public async Task A_cancelled_search_throws()
    {
        using GameFolder game = new();
        _ = game.Exe("Game.exe");
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => AntiCheat.DetectAsync(game.Root, game.Root, cancelled.Token));
    }
}
