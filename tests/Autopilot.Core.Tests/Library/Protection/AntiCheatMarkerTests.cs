using Autopilot.Core.Library.Protection;

namespace Autopilot.Core.Tests.Library.Protection;

[TestClass]
public sealed class AntiCheatMarkerTests(TestContext testContext) : ProtectionTestBase(testContext)
{
    [TestMethod]
    [DataRow("BEService.exe", "BattlEye")]
    [DataRow("BEClient_x64.dll", "BattlEye")]
    [DataRow("battleye.ini", "BattlEye")]
    [DataRow("EasyAntiCheat_Setup.exe", "Easy Anti-Cheat")]
    [DataRow("EAC_Launcher.exe", "Easy Anti-Cheat")]
    [DataRow("vgk.sys", "Riot Vanguard")]
    [DataRow("vgc.exe", "Riot Vanguard")]
    [DataRow("GameGuard.des", "nProtect GameGuard")]
    [DataRow("xigncode3.dll", "XIGNCODE3")]
    [DataRow("denuvo-anti-cheat.sys", "Denuvo Anti-Cheat")]
    [DataRow("DenuvoAntiCheat.dll", "Denuvo Anti-Cheat")]
    [DataRow("PunkBuster.exe", "PunkBuster")]
    [DataRow("FACEIT.exe", "FACEIT AC")]
    [DataRow("ricochet.sys", "Ricochet")]
    [DataRow("cod.exe", "Ricochet")]
    [DataRow("EAAntiCheat.GameServiceLauncher.exe", "EA Javelin")]
    [DataRow("EA_AntiCheat.dll", "EA Javelin")]
    [DataRow("ZenlessZoneZero.exe", "HoYoverse anti-cheat")]
    [DataRow("GenshinImpact.exe", "HoYoverse anti-cheat")]
    [DataRow("StarRail.exe", "HoYoverse anti-cheat")]
    [DataRow("mhyprot2.sys", "HoYoverse anti-cheat")]
    [DataRow("Elytra-Setup.exe", "Elytra Anti-Cheat")]
    [DataRow("ACE-Base64.dll", "ACE (Anti-Cheat Expert)")]
    [DataRow("AntiCheatExpert.dll", "ACE (Anti-Cheat Expert)")]
    [DataRow("Endfield.exe", "ACE (Anti-Cheat Expert)")]
    public async Task A_marker_file_names_its_product_and_is_the_evidence(string fileName, string product)
    {
        using GameFolder game = new();
        _ = game.Exe(fileName);

        AntiCheatFinding finding = await AntiCheat.DetectAsync(game.Root, game.Root, Ct);

        Assert.AreEqual(AntiCheatVerdict.Present, finding.Verdict);
        AssertNames(finding.Products, product);
        AssertNames(finding.Evidence, fileName);
    }

    [TestMethod]
    [DataRow("beservice.exe")]
    [DataRow("BEService.EXE")]
    [DataRow("my.battleye.ini")]
    [DataRow("easyanticheat-eos_setup.exe")]
    [DataRow("faceit2.dll")]
    [DataRow("mhyprot3.sys")]
    [DataRow("VGK.SYS")]
    [DataRow("COD.EXE")]
    public async Task A_marker_is_found_in_any_case_between_separators_and_before_digits(string fileName)
    {
        using GameFolder game = new();
        _ = game.Exe(fileName);

        AntiCheatFinding finding = await AntiCheat.DetectAsync(game.Root, game.Root, Ct);

        Assert.AreEqual(AntiCheatVerdict.Present, finding.Verdict, fileName);
    }

    [TestMethod]
    [DataRow("vanguard_outfit.pak")]
    [DataRow("Vanguard")]
    [DataRow("denuvo64.dll")]
    [DataRow("RiceRicochetFX.bin")]
    [DataRow("gameguardians.txt")]
    [DataRow("xfaceit.dll")]
    [DataRow("faceitx.dll")]
    [DataRow("faceit2x.dll")]
    [DataRow("1faceit.dll")]
    [DataRow("decode.exe")]
    [DataRow("cod.exe.bak")]
    [DataRow("vgk.sys.bak")]
    [DataRow("myvgc.exe")]
    public async Task A_marker_inside_a_longer_word_or_a_file_name_that_only_contains_one_is_not_anti_cheat(string fileName)
    {
        using GameFolder game = new();
        _ = game.Exe("ROTTR.exe").Exe(fileName);

        AntiCheatFinding finding = await AntiCheat.DetectAsync(game.Root, game.Root, Ct);

        Assert.AreEqual(AntiCheatVerdict.None, finding.Verdict, fileName);
        Assert.IsEmpty(finding.Products);
        Assert.IsEmpty(finding.Evidence);
    }

    [TestMethod]
    public async Task A_single_player_game_with_vanguard_and_denuvo_in_asset_names_has_no_anti_cheat_until_the_drivers_appear()
    {
        using GameFolder game = new("Rise of the Tomb Raider");
        _ = game.Exe("ROTTR.exe").Exe("vanguard_outfit.pak").Exe("denuvo64.dll").Exe("RiceRicochetFX.bin").Folder("Vanguard");
        Assert.AreEqual(AntiCheatVerdict.None, (await AntiCheat.DetectAsync(game.Root, game.Root, Ct)).Verdict);

        _ = game.Exe("vgk.sys").Exe("mhyprot3.sys");
        AntiCheatFinding finding = await AntiCheat.DetectAsync(game.Root, game.Root, Ct);

        AssertNames(finding.Products, "HoYoverse anti-cheat", "Riot Vanguard");
        AssertNames(finding.Evidence, "mhyprot3.sys", "vgk.sys");
    }

    [TestMethod]
    public async Task Products_and_evidence_are_sorted_and_each_appears_once()
    {
        using GameFolder game = new();
        _ = game.Exe("ZenlessZoneZero.exe").Exe("EAAntiCheat.GameServiceLauncher.exe").Exe("Anti/EAAntiCheat.GameServiceLauncher.exe");

        AntiCheatFinding finding = await AntiCheat.DetectAsync(game.Root, game.Root, Ct);

        Assert.AreEqual(AntiCheatVerdict.Present, finding.Verdict);
        AssertNames(finding.Products, "EA Javelin", "HoYoverse anti-cheat");
        AssertNames(finding.Evidence, "EAAntiCheat.GameServiceLauncher.exe", "ZenlessZoneZero.exe");
    }

    [TestMethod]
    public async Task Evidence_is_at_most_four_names_the_first_in_order_while_every_product_is_listed()
    {
        using GameFolder game = new();
        foreach (string name in new[] { "gameguard.des", "faceit.exe", "easyanticheat.exe", "beservice.exe", "beclient.dll", "battleye.ini" })
        {
            _ = game.Exe(name);
        }

        AntiCheatFinding finding = await AntiCheat.DetectAsync(game.Root, game.Root, Ct);

        AssertNames(finding.Evidence, "battleye.ini", "beclient.dll", "beservice.exe", "easyanticheat.exe");
        AssertNames(finding.Products, "BattlEye", "Easy Anti-Cheat", "FACEIT AC", "nProtect GameGuard");
    }

    [TestMethod]
    public async Task A_folder_that_is_an_anti_cheat_by_name_is_found_with_the_installer_inside_it()
    {
        using GameFolder game = new();
        _ = game.Exe("Elytra/Elytra-Setup.exe").Exe("WardogsLauncher-Shipping.exe");

        AntiCheatFinding finding = await AntiCheat.DetectAsync(Path.Join(game.Root, "Binaries"), game.Root, Ct);

        Assert.AreEqual(AntiCheatVerdict.Present, finding.Verdict, "found in the game folder, though the Binaries folder does not exist");
        AssertNames(finding.Products, "Elytra Anti-Cheat");
        AssertNames(finding.Evidence, "Elytra", "Elytra-Setup.exe");
    }

    [TestMethod]
    public async Task An_anti_cheat_in_a_subfolder_with_an_ordinary_name_is_found_by_its_files()
    {
        using GameFolder game = new();
        _ = game.Exe("Game.exe").Exe("Protection/EasyAntiCheat_Setup.exe");

        AntiCheatFinding finding = await AntiCheat.DetectAsync(game.Root, game.Root, Ct);

        Assert.AreEqual(AntiCheatVerdict.Present, finding.Verdict);
        AssertNames(finding.Evidence, "EasyAntiCheat_Setup.exe");
    }

    [TestMethod]
    public async Task Folders_two_levels_down_are_not_searched()
    {
        using GameFolder game = new();
        _ = game.Exe("Game.exe").Exe("one/two/battleye.exe");

        AntiCheatFinding finding = await AntiCheat.DetectAsync(game.Root, game.Root, Ct);

        Assert.AreEqual(AntiCheatVerdict.None, finding.Verdict);
    }

    [TestMethod]
    public async Task A_subfolder_is_searched_through_its_first_80_entries_in_name_order()
    {
        using GameFolder game = new();
        for (int i = 0; i < 80; i++)
        {
            _ = game.Exe($"full/file{i:D2}.dat");
            _ = game.Exe($"short/file{i:D2}.dat");
        }

        _ = game.Exe("full/zebra-faceit.dll");
        File.Delete(game.PathOf("short/file00.dat"));
        _ = game.Exe("short/zebra-beservice.dll");

        AntiCheatFinding finding = await AntiCheat.DetectAsync(game.Root, game.Root, Ct);

        AssertNames(finding.Evidence, "zebra-beservice.dll");
    }

    [TestMethod]
    public async Task A_hidden_subfolder_is_not_searched_and_its_visible_sibling_is()
    {
        using GameFolder game = new();
        _ = game.Exe(".cache/battleye.dll").Exe("Visible/beservice.dll");

        AntiCheatFinding finding = await AntiCheat.DetectAsync(game.Root, game.Root, Ct);

        AssertNames(finding.Evidence, "beservice.dll");
    }

    [TestMethod]
    public async Task A_subfolder_with_the_hidden_attribute_is_not_searched()
    {
        using GameFolder game = new();
        _ = game.Exe("Hidden/battleye.dll").Exe("Visible/beservice.dll");
        File.SetAttributes(game.PathOf("Hidden"), FileAttributes.Hidden);
        if (!File.GetAttributes(game.PathOf("Hidden")).HasFlag(FileAttributes.Hidden))
        {
            Assert.Inconclusive("this system cannot give a folder the hidden attribute");
        }

        AntiCheatFinding finding = await AntiCheat.DetectAsync(game.Root, game.Root, Ct);

        AssertNames(finding.Evidence, "beservice.dll");
    }

    [TestMethod]
    public async Task A_hidden_file_in_the_game_folder_still_counts()
    {
        using GameFolder game = new();
        _ = game.Exe(".battleye");

        AntiCheatFinding finding = await AntiCheat.DetectAsync(game.Root, game.Root, Ct);

        Assert.AreEqual(AntiCheatVerdict.Present, finding.Verdict);
    }

    [TestMethod]
    public async Task The_install_folder_and_the_game_folder_are_both_searched_and_merged()
    {
        using GameFolder game = new();
        _ = game.Exe("Binaries/Win64/Game.exe").Exe("Binaries/Win64/vgc.exe").Exe("faceit.exe").Exe("Binaries/shared/beservice.exe");

        AntiCheatFinding finding = await AntiCheat.DetectAsync(game.PathOf("Binaries/Win64"), game.Root, Ct);

        Assert.AreEqual(AntiCheatVerdict.Present, finding.Verdict);
        AssertNames(finding.Products, "FACEIT AC", "Riot Vanguard");
        AssertNames(finding.Evidence, "faceit.exe", "vgc.exe");
    }

    [TestMethod]
    public async Task The_same_folder_given_twice_in_other_spellings_gives_the_same_answer()
    {
        using GameFolder game = new();
        _ = game.Exe("faceit.exe");

        AntiCheatFinding once = await AntiCheat.DetectAsync(game.Root, game.Root, Ct);
        AntiCheatFinding twice = await AntiCheat.DetectAsync(game.Root + Path.DirectorySeparatorChar, game.Root.ToUpperInvariant(), Ct);

        AssertNames(twice.Evidence, [.. once.Evidence]);
        Assert.AreEqual(AntiCheatVerdict.Present, once.Verdict);
    }
}
