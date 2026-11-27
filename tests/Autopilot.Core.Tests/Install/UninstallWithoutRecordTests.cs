using System.Text;
using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>Uninstall with the record missing, damaged or the wrong shape: a known-names cleanup that never deletes the game's own files by name.</summary>
[TestClass]
public sealed class UninstallWithoutRecordTests(TestContext testContext)
{
    private static readonly byte[] _gameD3d11 = GameFolders.Dll("the game's d3d11");
    private static readonly byte[] _gameDlss = GameFolders.Dll("the game's dlss");

    private static readonly TreeFile[] _unreal =
    [
        TreeFile.Bytes("Binaries/Win64/Game.exe", GameFolders.Dll("the game")),
        TreeFile.Bytes("Engine/Plugins/DLSS/nvngx_dlss.dll", GameFolders.Dll("ours")),
        TreeFile.Bytes("Engine/Plugins/DLSS/nvngx_dlss.dll" + KnownFiles.BackupSuffix, _gameDlss),
    ];

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task Names_unique_to_the_tools_components_are_deleted_and_the_cleanup_says_how_it_worked()
    {
        using TransactionBed bed = new(
            TreeFile.Text("dlss5-feed.addon64", "ours"),
            TreeFile.Text("dlss5-feed.cfg", "ours"),
            TreeFile.Text("reshade-shaders/Shaders/DLSS5_Feed.fx", "ours"),
            TreeFile.Text("plugins/RTX40MFG.asi", "ours"),
            TreeFile.Text("Game.exe", "the game"));

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.IsFalse(bed.Exists("dlss5-feed.addon64") || bed.Exists("dlss5-feed.cfg") || bed.Exists("reshade-shaders/Shaders/DLSS5_Feed.fx") || bed.Exists("plugins/RTX40MFG.asi"));
        Assert.IsTrue(bed.Exists("Game.exe"));
        Assert.IsTrue(finished.WithoutRecord);
        Assert.AreEqual("No install record found; cleaning up by known filenames.", finished.Lines[0]);
        Assert.AreEqual(4, finished.Removed);
        CollectionAssert.Contains(finished.Lines.ToArray(), "removed: reshade-shaders/Shaders/DLSS5_Feed.fx");
    }

    [TestMethod]
    public async Task A_proxy_goes_only_when_its_content_says_it_is_a_component_and_the_games_own_stays()
    {
        using TransactionBed bed = new(
            TreeFile.Bytes("dxgi.dll", Big(ContentMarks.ReShade)),
            TreeFile.Bytes("d3d11.dll", _gameD3d11),
            TreeFile.Bytes("d3d12.dll", Big("nothing of ours")),
            TreeFile.Bytes("d3d8.dll", Big(ContentMarks.Dxvk)),
            TreeFile.Bytes("dinput8.dll", Big(ContentMarks.Reframework)),
            TreeFile.Bytes("bin/win64/d3d9.dll", Big(ContentMarks.Dxvk)),
            TreeFile.Bytes("opengl32.dll", Encoding.ASCII.GetBytes(ContentMarks.ReShade)));

        _ = await bed.UninstallFinishedAsync(Ct);

        Assert.IsFalse(bed.Exists("dxgi.dll") || bed.Exists("d3d8.dll") || bed.Exists("dinput8.dll") || bed.Exists("bin/win64/d3d9.dll"));
        Assert.IsTrue(bed.Holds("d3d11.dll", _gameD3d11));
        Assert.IsTrue(bed.Exists("d3d12.dll"));
        Assert.IsTrue(bed.Exists("opengl32.dll"), "a file under 1 MB is not ReShade, whatever it says");
    }

    [TestMethod]
    public async Task Names_games_ship_themselves_stay_with_a_line_saying_so()
    {
        using TransactionBed bed = new(TreeFile.Bytes("nvngx_dlss.dll", _gameDlss), TreeFile.Text("ReShade.ini", "mine"));

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.IsTrue(bed.Holds("nvngx_dlss.dll", _gameDlss));
        Assert.IsTrue(bed.Exists("ReShade.ini"));
        UninstallBed.AssertSaid(finished, "not removed: nvngx_dlss.dll: could not tell it from yours", "not removed: ReShade.ini: could not tell it from yours");
        Assert.AreEqual(0, finished.Removed);
    }

    [TestMethod]
    public async Task A_d3d9_dll_beside_a_Remix_runtime_is_left_alone()
    {
        using TransactionBed bed = new([.. GameFolders.Remix(RtxConf.FinalNewline), TreeFile.Bytes("d3d9.dll", Big(ContentMarks.Dxvk))]);

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.IsTrue(bed.Exists("d3d9.dll"));
        UninstallBed.AssertSaid(finished, "RTX Remix is installed here: d3d9.dll is its runtime, left alone.");
    }

    [TestMethod]
    public async Task Backups_with_the_tools_suffixes_are_put_back_and_the_log_warns_they_had_no_record()
    {
        using TransactionBed bed = new(
            TreeFile.Bytes("dxgi.dll", Big(ContentMarks.ReShade)),
            TreeFile.Bytes("dxgi.dll" + KnownFiles.BackupSuffix, Switches.GameDxgi),
            TreeFile.Bytes("d3dcompiler_47.dll" + KnownFiles.DlssOffSuffix, Switches.GameCompiler),
            TreeFile.Bytes("Plugins/DLSS/nvngx_dlss.dll", GameFolders.Dll("ours")),
            TreeFile.Bytes("Plugins/DLSS/nvngx_dlss.dll" + KnownFiles.BackupSuffix, _gameDlss));

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.IsTrue(bed.Holds("dxgi.dll", Switches.GameDxgi) && bed.Holds("d3dcompiler_47.dll", Switches.GameCompiler) && bed.Holds("Plugins/DLSS/nvngx_dlss.dll", _gameDlss));
        UninstallBed.AssertSaid(finished, "restored from a backup with no record; if the game updated since, verify its files", "restored: dxgi.dll (the game's own file)");
    }

    [TestMethod]
    public async Task A_swapped_runtimes_backup_elsewhere_in_the_game_folder_is_put_back()
    {
        using TransactionBed bed = new(_unreal) { InstallFolder = "Binaries/Win64" };

        _ = await bed.UninstallFinishedAsync(Ct);

        Assert.IsTrue(bed.Holds("Engine/Plugins/DLSS/nvngx_dlss.dll", _gameDlss));
        Assert.IsFalse(bed.Exists("Engine/Plugins/DLSS/nvngx_dlss.dll" + KnownFiles.BackupSuffix));
    }

    [TestMethod]
    [DataRow("not json at all")]
    [DataRow(/*lang=json,strict*/ "{\"version\": 1, \"state\": 7}")]
    public async Task A_damaged_record_falls_back_to_known_names_and_is_kept_aside_as_evidence(string damaged)
    {
        using TransactionBed bed = new(TreeFile.Text(KnownFiles.InstallRecord, damaged), TreeFile.Text("dlss5-feed.addon64", "ours"));

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.IsFalse(bed.Exists("dlss5-feed.addon64"));
        Assert.IsFalse(bed.Exists(KnownFiles.InstallRecord));
        Assert.AreEqual(damaged, bed.Read(KnownFiles.InstallRecord + KnownFiles.CorruptSuffix));
        Assert.AreEqual("The install record was damaged: it is kept as invidia-claritatis.json.corrupt, and nothing in it was trusted.", finished.Lines[0]);
        Assert.IsTrue(finished.WithoutRecord);
    }

    [TestMethod]
    public async Task A_second_damaged_record_is_kept_beside_the_first()
    {
        using TransactionBed bed = new(TreeFile.Text(KnownFiles.InstallRecord, "damaged again"), TreeFile.Text(KnownFiles.InstallRecord + KnownFiles.CorruptSuffix, "damaged first"));

        _ = await bed.UninstallFinishedAsync(Ct);

        Assert.AreEqual("damaged first", bed.Read(KnownFiles.InstallRecord + KnownFiles.CorruptSuffix));
        Assert.AreEqual("damaged again", bed.Read(KnownFiles.InstallRecord + KnownFiles.CorruptSuffix + ".2"));
    }

    [TestMethod]
    [DataRow("dlss5-dx11-bridge.addon64")]
    [DataRow("host64/dlss5-feed-host64.exe")]
    [DataRow("plugins/RTX40MFG.asi")]
    [DataRow("reshade-shaders/Shaders/DLSS5_Feed.fx")]
    public async Task Each_components_file_goes_by_its_name(string path)
    {
        using TransactionBed bed = new(TreeFile.Text(path, "ours"));

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.IsFalse(bed.Exists(path));
        UninstallBed.AssertSaid(finished, "removed: " + path);
    }

    [TestMethod]
    [DataRow("dlss5-feed.addon64")]
    [DataRow("dxgi.dll")]
    public async Task A_file_another_program_holds_is_said_and_counted_as_left(string held)
    {
        using TransactionBed bed = new(
            TreeFile.Text("dlss5-feed.addon64", "ours"),
            TreeFile.Text("dxgi.dll", "ours"),
            TreeFile.Bytes("dxgi.dll" + KnownFiles.BackupSuffix, Switches.GameDxgi));
        _ = bed.Hold(held);

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.AreEqual(1, finished.Left);
        Assert.IsTrue(bed.Exists(held));
        UninstallBed.AssertSaidStarting(finished, $"could not remove: {held}");
    }

    // A DLL over 1 MB carrying mark past its first megabyte, as a component's DLL does somewhere in its bytes.
    private static byte[] Big(string mark) => [.. GameFolders.Dll(), .. new byte[1 << 20], .. Encoding.ASCII.GetBytes(mark)];

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task The_helper_folder_goes_once_empty_and_stays_while_anything_is_in_it(bool somethingElseInside)
    {
        using TransactionBed bed = new(TreeFile.Text("host64/dlss5-feed-host64.exe", "ours"));
        if (somethingElseInside)
        {
            bed.Plant("host64/notes.txt");
        }

        _ = await bed.UninstallFinishedAsync(Ct);

        Assert.IsFalse(bed.Exists("host64/dlss5-feed-host64.exe"));
        Assert.AreEqual(somethingElseInside, bed.HasFolder("host64"));
    }
}
