using Autopilot.Core.Library.Protection;

namespace Autopilot.Core.Tests.Library.Protection;

[TestClass]
public sealed class AntiCheatTextTests
{
    [TestMethod]
    public void The_warning_names_the_products_and_the_files_it_rests_on()
    {
        AntiCheatFinding finding = new(AntiCheatVerdict.Present, ["BattlEye", "Easy Anti-Cheat"], ["beservice.exe", "easyanticheat.exe"]);

        string text = finding.Explain();

        Assert.StartsWith("BattlEye, Easy Anti-Cheat is installed with this game.", text);
        Assert.Contains("Anti-cheat and ReShade add-ons do not coexist.", text);
        Assert.Contains("If you play this game online, do not install here.", text);
        Assert.EndsWith("(found: beservice.exe, easyanticheat.exe)", text);
    }

    [TestMethod]
    public void An_answer_that_could_not_be_read_says_so_and_still_names_what_was_found()
    {
        AntiCheatFinding finding = new(AntiCheatVerdict.Unknown, ["FACEIT AC"], ["faceit.exe"]);

        string text = finding.Explain();

        Assert.Contains("could not read every folder", text);
        Assert.Contains("treats the game as having one", text);
        Assert.EndsWith("(found: faceit.exe)", text);
        Assert.Contains("could not read every folder", new AntiCheatFinding(AntiCheatVerdict.Unknown, [], []).Explain());
        Assert.DoesNotContain("found:", new AntiCheatFinding(AntiCheatVerdict.Unknown, [], []).Explain());
    }

    [TestMethod]
    public void No_anti_cheat_has_nothing_to_warn_about() =>
        Assert.AreEqual(string.Empty, new AntiCheatFinding(AntiCheatVerdict.None, [], []).Explain());

    [TestMethod]
    public void The_swap_warning_names_the_file_and_says_what_is_kept()
    {
        string text = AntiCheat.SwapWarning("nvngx_dlss.dll");

        Assert.StartsWith("Swapping nvngx_dlss.dll replaces a file the game itself ships.", text);
        Assert.Contains("comes back when you uninstall", text);
        Assert.Contains("For anything you play online, keep the game's own file.", text);
    }
}
