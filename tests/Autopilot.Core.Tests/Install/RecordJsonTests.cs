using System.Text.Json.Nodes;

namespace Autopilot.Core.Tests.Install;

/// <summary>The record's JSON: camelCase keys, enums by their kebab-case names, the route by its stable id.</summary>
[TestClass]
public sealed class RecordJsonTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task The_record_names_its_state_game_and_route()
    {
        using RecordBed bed = new();

        await bed.SaveAsync(FullRecord.Value with { InstallFolder = "" }, Ct);
        JsonObject json = bed.OnDisk();

        Assert.AreEqual("installing", (string?)json["state"]);
        Assert.AreEqual("optiscaler", (string?)json["route"]);
        Assert.AreEqual("battle-net", (string?)json["game"]!["kind"]);
        Assert.AreEqual("dx12", (string?)json["game"]!["api"]);
        Assert.AreEqual("open-xr", (string?)json["registrations"]![1]!["layer"]);
        Assert.AreEqual("", (string?)json["installFolder"]);
    }

    [TestMethod]
    public async Task Entries_name_their_class_state_attributes_and_backup()
    {
        using RecordBed bed = new();

        await bed.SaveAsync(FullRecord.Value with { InstallFolder = "" }, Ct);
        JsonArray entries = bed.OnDisk()["entries"]!.AsArray();

        Assert.AreEqual("read-only, hidden", (string?)entries[0]!["original"]!["attributes"]);
        Assert.AreEqual("2025-03-04T05:06:07Z", (string?)entries[0]!["original"]!["lastWriteTimeUtc"]);
        Assert.AreEqual("config", (string?)entries[1]!["class"]);
        Assert.AreEqual("moved-aside", (string?)entries[2]!["state"]);
        Assert.AreEqual("dlss5-off", (string?)entries[2]!["original"]!["backup"]);
        Assert.AreEqual("restore-failed", (string?)entries[3]!["state"]);
    }

    [TestMethod]
    public async Task An_open_install_names_its_held_files_and_carries_the_previous_record()
    {
        using RecordBed bed = new();

        await bed.SaveAsync(FullRecord.Value with { InstallFolder = "" }, Ct);
        JsonObject json = bed.OnDisk();

        Assert.AreEqual("held", (string?)json["entries"]![5]!["state"]);
        Assert.AreEqual("native", (string?)json["previous"]!["route"]);
        Assert.IsNull(json["previous"]!["previous"]);
    }

    [TestMethod]
    public async Task An_open_install_names_the_originals_it_renamed_and_where_they_went()
    {
        using RecordBed bed = new();

        await bed.SaveAsync(FullRecord.Value with { InstallFolder = "" }, Ct);
        JsonObject entry = bed.OnDisk()["entries"]![6]!.AsObject();

        Assert.AreEqual("restored", (string?)entry["state"]);
        Assert.AreEqual(2, (int?)entry["superseded"]);
    }
}
