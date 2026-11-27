using System.Text.Json.Nodes;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Install;
using Autopilot.Core.Routes;

namespace Autopilot.Core.Tests.Install;

/// <summary>The settings an install read, as its record keeps them: typed, enums by name, numbers refused.</summary>
[TestClass]
public sealed class RecordChoicesTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task Every_setting_comes_back_as_it_was_saved()
    {
        using RecordBed bed = new();

        await bed.SaveAsync(Recorded.Install() with { Choices = FullChoices.Value }, Ct);

        Assert.AreEqual(FullChoices.Value, (await bed.FoundAsync("", Ct)).Record!.Choices);
    }

    [TestMethod]
    public async Task Settings_are_named_as_people_read_them()
    {
        using RecordBed bed = new();

        await bed.SaveAsync(Recorded.Install() with { Choices = FullChoices.Value }, Ct);
        JsonObject choices = bed.OnDisk()["choices"]!.AsObject();

        Assert.AreEqual("wilsjo2-mfg", (string?)choices["optiScalerBuild"]);
        Assert.AreEqual("built-in", (string?)choices["motionProvider"]);
        Assert.AreEqual("sm86-dbghelp", (string?)choices["ownFrameGeneration"]);
        Assert.AreEqual("d3d12", (string?)choices["reShadeLoadsAs"]);
        Assert.AreEqual("preset2", (string?)choices["optiScalerPreset"]);
        Assert.AreEqual("exact-tag", (string?)choices["feederBuild"]!["pick"]);
        Assert.AreEqual("v2.0.0", (string?)choices["feederBuild"]!["tag"]);
    }

    [TestMethod]
    public async Task Quantmotion_round_trips_by_name_so_the_install_can_refuse_it()
    {
        using RecordBed bed = new();

        bed.PlantEdited(Recorded.Install(), json => json["choices"]!["motionProvider"] = "quant-motion");

        Assert.AreEqual(MotionProvider.QuantMotion, (await bed.FoundAsync("", Ct)).Record!.Choices.MotionProvider);
    }

    [TestMethod]
    [DataRow("motionProvider", "4", DisplayName = "provider 4 as a number")]
    [DataRow("feederHdr", "1", DisplayName = "an enum as a number")]
    [DataRow("optiScalerBuild", "\"pocket\"", DisplayName = "a build no one has")]
    [DataRow("keepGameDlss", "null", DisplayName = "null for a box")]
    [DataRow("feederBuild", /*lang=json,strict*/ "{ \"pick\": \"exact-tag\", \"tag\": null }", DisplayName = "an exact pick without its tag")]
    [DataRow("feederBuild", /*lang=json,strict*/ "{ \"pick\": \"newest-release\", \"tag\": \"v1\" }", DisplayName = "a newest pick with a tag")]
    [DataRow("modelResolution", "20", DisplayName = "a model resolution below 25")]
    [DataRow("modelResolution", "72", DisplayName = "a model resolution off the steps of 5")]
    [DataRow("overlayKey", "256", DisplayName = "an overlay key no Windows key has")]
    public async Task A_setting_of_the_wrong_shape_makes_the_record_unreadable(string key, string json)
    {
        using RecordBed bed = new();

        bed.PlantEdited(Recorded.Install(), record => record["choices"]![key] = JsonNode.Parse(json));

        await bed.AssertUnreadableAsync(RecordProblem.WrongShape, Ct);
    }

    [TestMethod]
    public async Task A_setting_the_record_does_not_hold_reads_as_its_default()
    {
        using RecordBed bed = new();

        bed.PlantEdited(Recorded.Install(), json => json["choices"] = new JsonObject());

        Assert.AreEqual(RouteSettings.Default, (await bed.FoundAsync("", Ct)).Record!.Choices);
    }
}
