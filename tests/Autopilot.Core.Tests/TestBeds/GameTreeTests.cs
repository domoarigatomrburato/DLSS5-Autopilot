namespace Autopilot.Core.Tests.TestBeds;

/// <summary>Game folders from a description and tree snapshots, the beds the install tests stand on, do what they say.</summary>
[TestClass]
public sealed class GameTreeTests
{
    private static readonly DateTimeOffset _old = new(2024, 5, 1, 8, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void A_game_tree_is_written_as_described_and_deleted_whole()
    {
        string root;
        using (GameTree game = new("Some Game"))
        {
            root = game.Root;
            _ = game.Add(
                TreeFile.Text(@"Binaries\Win64/settings.ini", "a=1"),
                TreeFile.Sized("big.pak", 5_000_000) with { ReadOnly = true, Modified = _old }).Folder("Saved");

            Assert.AreEqual("Some Game", Path.GetFileName(root));
            Assert.AreEqual("a=1", File.ReadAllText(Path.Join(root, "Binaries", "Win64", "settings.ini")));
            FileInfo big = new(game.PathOf("big.pak"));
            Assert.AreEqual(5_000_000, big.Length);
            Assert.IsTrue(big.IsReadOnly);
            Assert.AreEqual(_old.UtcDateTime, big.LastWriteTimeUtc);
            Assert.IsTrue(Directory.Exists(game.PathOf("Saved")));
        }

        Assert.IsFalse(Directory.Exists(root));
    }

    [TestMethod]
    public void The_presets_hold_install_md_s_game_folders()
    {
        using var game = GameTree.Of([.. GameFolders.OwnFiles, .. GameFolders.SecondReShade, .. GameFolders.Remix(RtxConf.KeyFalse), .. GameFolders.Unreal]);

        string[] expected =
        [
            ".trex/d3d9.dll", "Binaries/Win64/game.exe", "d3d11.dll", "d3dcompiler_47.dll", "dxgi.dll", "Game.exe",
            "nvngx_dlss.dll", "Plugins/DLSS/Binaries/ThirdParty/Win64/nvngx_dlss.dll", "ReShade.ini", "rtx.conf",
        ];
        CollectionAssert.AreEquivalent(expected, TreeSnapshot.Of(game.Root).Paths.Select(path => path.Replace('\\', '/')).ToArray());
        StringAssert.Contains(File.ReadAllText(game.PathOf("rtx.conf")), GameFolders.RtxNeuralKey + " = False", StringComparison.Ordinal);
        StringAssert.EndsWith(File.ReadAllText(game.PathOf("d3d11.dll")), Markers.ReShade, StringComparison.Ordinal);
        Assert.IsEmpty(GameFolders.Empty);
    }

    [TestMethod]
    public void The_remix_conf_comes_with_and_without_a_final_newline()
    {
        Assert.EndsWith("\n", Conf(RtxConf.FinalNewline));
        Assert.DoesNotEndWith("\n", Conf(RtxConf.NoFinalNewline));
        Assert.DoesNotContain(GameFolders.RtxNeuralKey, Conf(RtxConf.FinalNewline));
    }

    [TestMethod]
    public void A_hand_installed_optiscaler_carries_its_marker_under_the_proxy_name()
    {
        using var game = GameTree.Of(GameFolders.HandInstalledOptiScaler);

        StringAssert.EndsWith(File.ReadAllText(game.PathOf("dxgi.dll")), Markers.OptiScaler, StringComparison.Ordinal);
        Assert.IsTrue(File.Exists(game.PathOf("OptiScaler.ini")));
    }

    [TestMethod]
    public void A_snapshot_names_every_path_added_removed_or_changed_and_compares_paths_ignoring_case()
    {
        using var game = GameTree.Of(
            TreeFile.Text("a.txt", "a"),
            TreeFile.Text("b.txt", "b"),
            TreeFile.Text("c.txt", "c") with { Modified = _old },
            TreeFile.Text("d.txt", "d"),
            TreeFile.Text("e.txt", "e"));
        var before = TreeSnapshot.Of(game.Root);

        File.Move(game.PathOf("a.txt"), game.PathOf("A.TXT.tmp"));
        File.Move(game.PathOf("A.TXT.tmp"), game.PathOf("A.TXT"));
        File.WriteAllText(game.PathOf("b.txt"), "B");
        File.SetLastWriteTimeUtc(game.PathOf("c.txt"), _old.UtcDateTime.AddHours(1));
        File.SetAttributes(game.PathOf("d.txt"), FileAttributes.ReadOnly);
        File.Delete(game.PathOf("e.txt"));
        _ = game.Add(TreeFile.Text(@"new\f.txt", "f"));

        string? difference = before.Difference(TreeSnapshot.Of(game.Root));

        File.SetAttributes(game.PathOf("d.txt"), FileAttributes.Normal);
        StringAssert.StartsWith(difference, "added: new/f.txt; removed: e.txt; changed: b.txt (content", StringComparison.Ordinal);
        StringAssert.Contains(difference, "c.txt (modified time)", StringComparison.Ordinal);
        StringAssert.Contains(difference, "d.txt (attributes", StringComparison.Ordinal);
        Assert.DoesNotContain("a.txt", difference, StringComparison.OrdinalIgnoreCase);
        before.AssertSame(before);
        _ = Assert.ThrowsExactly<AssertFailedException>(() => before.AssertSame(TreeSnapshot.Of(game.Root)));
    }

    private static string Conf(RtxConf conf)
    {
        using var game = GameTree.Of(GameFolders.Remix(conf));
        return File.ReadAllText(game.PathOf("rtx.conf"));
    }
}
