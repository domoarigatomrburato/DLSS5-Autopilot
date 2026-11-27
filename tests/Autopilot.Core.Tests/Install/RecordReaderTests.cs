using System.Text.Json.Nodes;
using Autopilot.Core.Install;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>
/// What install's reader answers for a folder: no record, a record in its state, or unreadable with the
/// reason; a record that is there is never taken for none.
/// </summary>
[TestClass]
public sealed class RecordReaderTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_folder_without_a_record_has_none()
    {
        using RecordBed bed = new(GameFolders.OwnFiles);

        Assert.IsNull(await bed.FindAsync("", Ct));
    }

    [TestMethod]
    [DataRow(RecordState.Installing, InstallState.Installing)]
    [DataRow(RecordState.Installed, InstallState.Installed)]
    [DataRow(RecordState.Removing, InstallState.Removing)]
    public async Task A_record_reads_as_its_state(RecordState state, InstallState expected)
    {
        using RecordBed bed = new();
        Recorded.Plant(bed.Tree.Root, Recorded.Install() with { State = state });

        RecordedInstall found = await bed.FoundAsync("", Ct);

        Assert.AreEqual(expected, found.State);
        Assert.IsNull(found.Problem);
        Assert.AreEqual("Game.exe", string.Join("|", found.Executables));
    }

    [TestMethod]
    public async Task A_record_from_a_newer_version_is_unreadable_and_says_so()
    {
        using RecordBed bed = new();
        bed.PlantEdited(Recorded.Install(), json => json["version"] = 2);

        await bed.AssertUnreadableAsync(RecordProblem.Newer, Ct);
    }

    [TestMethod]
    [DataRow("{ \"version\": 1, \"state\": ")]
    [DataRow("[1, 2]")]
    [DataRow("\"installed\"")]
    [DataRow("")]
    public async Task A_record_that_is_not_a_json_object_is_damaged(string text)
    {
        using RecordBed bed = new();
        bed.PlantRaw(text);

        await bed.AssertUnreadableAsync(RecordProblem.Damaged, Ct);
    }

    [TestMethod]
    public async Task A_folder_where_the_record_should_be_is_damaged()
    {
        using RecordBed bed = new();
        _ = bed.Tree.Folder("invidia-claritatis.json");

        await bed.AssertUnreadableAsync(RecordProblem.Damaged, Ct);
    }

    [TestMethod]
    public async Task A_record_over_a_mebibyte_is_damaged()
    {
        using RecordBed bed = new();
        bed.PlantEdited(Recorded.Install(), json => json["notes"] = new JsonArray(new string('x', 1024 * 1024)));

        await bed.AssertUnreadableAsync(RecordProblem.Damaged, Ct);
    }

    [TestMethod]
    public async Task A_record_another_program_holds_is_unreadable_never_none()
    {
        using RecordBed bed = new();
        Recorded.Plant(bed.Tree.Root, Recorded.Install());

        using (new FileStream(bed.RecordPath(), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await bed.AssertUnreadableAsync(RecordProblem.Inaccessible, Ct);
        }

        Assert.AreEqual(InstallState.Installed, (await bed.FoundAsync("", Ct)).State, "read once it is let go");
    }

    [TestMethod]
    [DataRow("state")]
    [DataRow("version")]
    [DataRow("entries")]
    [DataRow("game")]
    [DataRow("installFolder")]
    [DataRow("choices")]
    public async Task A_record_missing_a_field_is_the_wrong_shape(string field)
    {
        using RecordBed bed = new();
        bed.PlantEdited(Recorded.Install(), json => json.Remove(field));

        await bed.AssertUnreadableAsync(RecordProblem.WrongShape, Ct);
    }

    [TestMethod]
    [DataRow("state", "1")]
    [DataRow("state", "\"finished\"")]
    [DataRow("version", "0")]
    [DataRow("route", "\"opti-scaler\"")]
    [DataRow("entries", "\"dxgi.dll\"")]
    [DataRow("entries", "[null]")]
    [DataRow("notes", "[\"done\", null]")]
    [DataRow("toolVersion", "null")]
    [DataRow("components", /*lang=json,strict*/ "{\"reshade\": null}")]
    [DataRow("entries", /*lang=json,strict*/ "[{\"path\": \"dxgi.dll\", \"class\": 0, \"state\": \"written\", \"written\": null, \"original\": null}]")]
    [DataRow("entries", /*lang=json,strict*/ "[{\"path\": null, \"class\": \"component\", \"state\": \"written\", \"written\": null, \"original\": null}]")]
    [DataRow("entries", /*lang=json,strict*/ "[{\"path\": \"dxgi.dll\", \"class\": \"component\", \"state\": \"written\", \"written\": null}]")]
    [DataRow("releasing", /*lang=json,strict*/ "{\"registrations\": [null], \"graphicsPreferences\": []}")]
    [DataRow("releasing", /*lang=json,strict*/ "{\"registrations\": [], \"graphicsPreferences\": [null]}")]
    public async Task A_record_with_a_field_of_the_wrong_type_is_the_wrong_shape(string field, string json)
    {
        using RecordBed bed = new();
        bed.PlantEdited(Recorded.Install(), record => record[field] = JsonNode.Parse(json));

        await bed.AssertUnreadableAsync(RecordProblem.WrongShape, Ct);
    }

    [TestMethod]
    [DataRow("Binaries/Win64", "")]
    [DataRow("Binaries/Win32", "Binaries/Win64")]
    [DataRow("Other/Binaries/Win64", "Binaries/Win64")]
    [DataRow("../Binaries/Win64", "Binaries/Win64")]
    [DataRow("./Win64", "Binaries/Win64")]
    [DataRow("Binaries//Win64", "Binaries/Win64")]
    [DataRow("/Binaries/Win64", "Binaries/Win64")]
    [DataRow("C:/Game/Binaries/Win64", "Binaries/Win64")]
    public async Task A_record_whose_install_folder_does_not_end_where_it_sits_is_the_wrong_shape(string recorded, string sits)
    {
        using RecordBed bed = new(GameFolders.Unreal);
        bed.PlantEdited(Recorded.Install(recorded), _ => { }, sits);

        await bed.AssertUnreadableAsync(RecordProblem.WrongShape, Ct, sits);
    }

    [TestMethod]
    public async Task A_record_that_widens_its_game_folder_to_the_root_is_the_wrong_shape()
    {
        using RecordBed bed = new(GameFolders.Unreal);
        string install = bed.Tree.PathOf("Binaries/Win64");
        // Every part of the install folder, so the game folder it names is the drive's root.
        Recorded.Plant(install, Recorded.Install(RootRelative(install)));

        await bed.AssertUnreadableAsync(RecordProblem.WrongShape, Ct, "Binaries/Win64");
    }

    [TestMethod]
    public async Task A_record_whose_game_folder_is_the_folder_above_the_game_still_reads()
    {
        using RecordBed bed = new(GameFolders.Unreal);
        string install = bed.Tree.PathOf("Binaries/Win64");
        Recorded.Plant(install, Recorded.Install("Game/Binaries/Win64"));

        Assert.AreEqual(InstallState.Installed, (await bed.FoundAsync("Binaries/Win64", Ct)).State);
    }

    [TestMethod]
    [DataRow("binaries\\WIN64")]
    [DataRow("Binaries/Win64")]
    public async Task The_install_folder_matches_ignoring_case_and_with_either_slash(string recorded)
    {
        using RecordBed bed = new(GameFolders.Unreal);
        Recorded.Plant(bed.Tree.PathOf("Binaries/Win64"), Recorded.Install(recorded));

        Assert.AreEqual(InstallState.Installed, (await bed.FoundAsync("Binaries/Win64", Ct)).State);
    }

    [TestMethod]
    public async Task Keys_this_version_does_not_know_are_ignored()
    {
        using RecordBed bed = new();
        bed.PlantEdited(Recorded.Install("", Recorded.Ours("dxgi.dll")), json =>
        {
            json["later"] = new JsonObject { ["anything"] = 1 };
            json["entries"]![0]!["checkedAt"] = "2027-01-01";
        });

        Assert.AreEqual("dxgi.dll", await bed.EntryPathsAsync("", Ct));
    }

    [TestMethod]
    public async Task A_cancelled_read_throws()
    {
        using RecordBed bed = new();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => bed.FindAsync("", new CancellationToken(canceled: true)));
    }

    // The install folder spelled relative to its root: on Windows its drive's root, elsewhere "/".
    private static string RootRelative(string folder)
    {
        string full = Path.GetFullPath(folder);
        return Path.GetRelativePath(Path.GetPathRoot(full)!, full).Replace('\\', '/');
    }
}
