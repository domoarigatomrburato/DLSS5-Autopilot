using Autopilot.Core.Install;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>The record's paths are untrusted: one that would leave the game folder is dropped with a log line and never followed, and reading writes nothing.</summary>
[TestClass]
public sealed class RecordEntriesTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    [DataRow("../outside.dll")]
    [DataRow("bin/../../outside.dll")]
    [DataRow("bin/../dxgi.dll")]
    [DataRow("/etc/passwd")]
    [DataRow("\\Windows\\System32\\d3d11.dll")]
    [DataRow("C:/Windows/System32/d3d11.dll")]
    [DataRow("C:d3d11.dll")]
    [DataRow("./d3d11.dll")]
    [DataRow("bin//d3d11.dll")]
    [DataRow("")]
    [DataRow("d3d11.dll:stream")]
    public async Task An_entry_that_would_leave_the_game_folder_is_dropped_with_a_log_line_and_the_rest_kept(string path)
    {
        using RecordBed bed = new();
        Recorded.Plant(bed.Tree.Root, Recorded.Install("", Recorded.Ours("dxgi.dll"), Recorded.Ours(path), Recorded.Ours("ReShade.ini")));

        Assert.AreEqual("dxgi.dll|ReShade.ini", await bed.EntryPathsAsync("", Ct));
        Assert.Contains(line => line.Contains(path, StringComparison.Ordinal) && line.Contains("not a path inside the game folder", StringComparison.Ordinal), bed.LogLines);
    }

    [TestMethod]
    public async Task Of_two_entries_naming_one_path_the_first_is_kept_with_a_log_line()
    {
        using RecordBed bed = new();
        RecordEntry first = Recorded.Ours("bin/dxgi.dll");
        Recorded.Plant(bed.Tree.Root, Recorded.Install("", first, Recorded.Swapped("BIN\\DXGI.DLL")));

        Assert.AreEqual(first, (await bed.FoundAsync("", Ct)).Record!.Entries.Single());
        Assert.Contains(line => line.Contains("BIN\\DXGI.DLL", StringComparison.Ordinal) && line.Contains("same path", StringComparison.Ordinal), bed.LogLines);
    }

    [TestMethod]
    public async Task Created_folders_follow_the_same_rule()
    {
        using RecordBed bed = new();
        Recorded.Plant(bed.Tree.Root, Recorded.Install() with { CreatedFolders = ["reshade-shaders", "../elsewhere", "RESHADE-SHADERS"] });

        Assert.AreEqual("reshade-shaders", string.Join("|", (await bed.FoundAsync("", Ct)).Record!.CreatedFolders));
        Assert.HasCount(2, bed.LogLines);
    }

    [TestMethod]
    public async Task Config_edits_follow_the_same_rule_by_file_section_and_key()
    {
        using RecordBed bed = new();
        ConfigEdit edit = new("rtx.conf", null, GameFolders.RtxNeuralKey, null, false, true, "True", true);
        ConfigEdit other = edit with { Key = "rtx.other" };
        Recorded.Plant(bed.Tree.Root, Recorded.Install() with
        {
            ConfigEdits = [edit, edit with { File = "../../AppData/settings.ini" }, edit with { File = "RTX.CONF", Value = "False" }, other],
        });

        IReadOnlyList<ConfigEdit> kept = (await bed.FoundAsync("", Ct)).Record!.ConfigEdits;

        Assert.HasCount(2, kept);
        Assert.AreEqual(edit, kept[0]);
        Assert.AreEqual(other, kept[1]);
        Assert.HasCount(2, bed.LogLines);
    }

    [TestMethod]
    public async Task A_key_outside_any_section_and_one_in_a_section_named_empty_are_two_edits()
    {
        using RecordBed bed = new();
        ConfigEdit unsectioned = new("retroarch.cfg", null, "video_driver", null, false, true, "\"d3d11\"", true);
        ConfigEdit emptySection = unsectioned with { Section = "" };
        Recorded.Plant(bed.Tree.Root, Recorded.Install() with { ConfigEdits = [unsectioned, emptySection] });

        Assert.HasCount(2, (await bed.FoundAsync("", Ct)).Record!.ConfigEdits);
        Assert.IsEmpty(bed.LogLines);
    }

    [TestMethod]
    public async Task In_an_unreal_layout_entries_under_the_game_folder_are_kept()
    {
        using RecordBed bed = new(GameFolders.Unreal);
        Recorded.Plant(
            bed.Tree.PathOf("Binaries/Win64"),
            Recorded.Install("Binaries/Win64", Recorded.Ours("Binaries/Win64/dxgi.dll"), Recorded.Swapped("Plugins/DLSS/Binaries/ThirdParty/Win64/nvngx_dlss.dll")));

        Assert.AreEqual("Binaries/Win64/dxgi.dll|Plugins/DLSS/Binaries/ThirdParty/Win64/nvngx_dlss.dll", await bed.EntryPathsAsync("Binaries/Win64", Ct));
        Assert.IsEmpty(bed.LogLines);
    }

    [TestMethod]
    public async Task Reading_never_writes()
    {
        using RecordBed bed = new(GameFolders.OwnFiles);
        Recorded.Plant(bed.Tree.Root, Recorded.Install("", Recorded.Ours("../outside.dll"), Recorded.Ours("dxgi.dll"), Recorded.Ours("DXGI.dll")));
        var before = TreeSnapshot.Of(bed.Tree.Root);

        _ = await bed.FindAsync("", Ct);
        before.AssertSame(TreeSnapshot.Of(bed.Tree.Root));

        bed.PlantRaw("{ damaged");
        var damaged = TreeSnapshot.Of(bed.Tree.Root);
        _ = await bed.FindAsync("", Ct);
        damaged.AssertSame(TreeSnapshot.Of(bed.Tree.Root));
    }
}
