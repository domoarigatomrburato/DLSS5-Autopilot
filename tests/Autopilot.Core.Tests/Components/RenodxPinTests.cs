using Autopilot.Core.Components;
using Autopilot.Core.Graphics;

namespace Autopilot.Core.Tests.Components;

[TestClass]
public sealed class RenodxPinTests(TestContext testContext)
{
    private const string Driver61656 = "616.56";
    private const string Driver61664 = "616.64";

    [TestMethod]
    [DataRow(Driver61664, "4.55", RenodxReason.DriverFault, DisplayName = "616.64")]
    [DataRow("616.92", "4.55", RenodxReason.DriverFault, DisplayName = "newer than 616.64")]
    [DataRow(Driver61656, "10.8.0", RenodxReason.Newest, DisplayName = "616.56")]
    [DataRow(null, "10.8.0", RenodxReason.Newest, DisplayName = "unknown driver")]
    public async Task On_616_64_and_newer_the_add_on_is_4_55(string? driver, string label, RenodxReason reason)
    {
        PickedBuild picked = await Pick(new(null, OwnFile: false, driver, GraphicsApi.DX12, FeederTag: null), reason);

        Assert.AreEqual(label, picked.Entry.Label);
    }

    [TestMethod]
    [DataRow(Driver61656, DisplayName = "616.56")]
    [DataRow(null, DisplayName = "unknown driver")]
    public async Task An_opengl_game_gets_4_60(string? driver)
    {
        PickedBuild picked = await Pick(new(null, OwnFile: false, driver, GraphicsApi.OpenGL, FeederTag: "v0.7.0"), RenodxReason.OpenGl);

        Assert.AreEqual("4.60", picked.Entry.Label);
    }

    [TestMethod]
    public async Task The_driver_pin_wins_over_opengl()
    {
        PickedBuild picked = await Pick(new(null, OwnFile: false, Driver61664, GraphicsApi.OpenGL, FeederTag: null), RenodxReason.DriverFault);

        Assert.AreEqual("4.55", picked.Entry.Label);
    }

    [TestMethod]
    [DataRow("v0.7.0", "4.55", RenodxReason.OldFeeder)]
    [DataRow("v0.8.0-beta.2", "4.55", RenodxReason.OldFeeder)]
    [DataRow("v0.8.0-beta.3", "10.8.0", RenodxReason.Newest)]
    [DataRow("v0.15.1", "10.8.0", RenodxReason.Newest)]
    public async Task A_feeder_below_0_8_0_beta_3_gets_4_55(string feeder, string label, RenodxReason reason)
    {
        PickedBuild picked = await Pick(new(null, OwnFile: false, Driver61656, GraphicsApi.DX11, feeder), reason);

        Assert.AreEqual(label, picked.Entry.Label);
    }

    [TestMethod]
    public async Task The_persons_choice_wins_over_every_pin()
    {
        PickedBuild picked = await Pick(new("5.2.1", OwnFile: false, Driver61664, GraphicsApi.OpenGL, "v0.7.0"), RenodxReason.PersonChoice);

        Assert.AreEqual("5.2.1", picked.Entry.Label);
    }

    [TestMethod]
    public async Task A_chosen_build_not_on_the_list_gives_the_newest_for_the_caller_to_say()
    {
        PickedBuild picked = await Pick(new("4.10", OwnFile: false, Driver61664, GraphicsApi.DX12, null), RenodxReason.PersonChoice);

        Assert.AreEqual("10.8.0", picked.Entry.Label);
        Assert.IsTrue(picked.WantedMissing);
    }

    [TestMethod]
    public async Task An_own_file_takes_nothing_from_the_list()
    {
        RenodxDecision decision = RenodxPins.Decide(new("4.55", OwnFile: true, Driver61664, GraphicsApi.OpenGL, null));
        BuildCatalog catalog = await MirrorBed.Captured(testContext);

        Assert.AreEqual(new RenodxDecision(RenodxReason.OwnFile, null), decision);
        _ = Assert.Throws<ArgumentException>(() => RenodxPins.Pick(catalog, decision));
    }

    [TestMethod]
    [DataRow(Driver61664, GraphicsApi.DX12, "4.55")]
    [DataRow(Driver61656, GraphicsApi.OpenGL, "4.60")]
    public async Task A_pin_not_on_the_list_stops_the_install(string driver, GraphicsApi api, string pin)
    {
        BuildCatalog catalog = await MirrorBed.Captured(testContext, "renodx-dlss5-4.55", "renodx-dlss5-4.60");
        RenodxDecision decision = RenodxPins.Decide(new(null, OwnFile: false, driver, api, null));

        ComponentException failure = Assert.Throws<ComponentException>(() => RenodxPins.Pick(catalog, decision));

        Assert.AreEqual(ComponentFailure.PinMissing, failure.Reason);
        Assert.AreEqual(pin, failure.Label);
        Assert.AreEqual(
            $"renodx-dlss5 {pin} is not on the build list; the newest build is the kind of build this pin exists to avoid. Pick the build by hand under 'dlss5 add-on' to go ahead.",
            failure.Message);
    }

    [TestMethod]
    public void Each_pin_carries_its_evidence()
    {
        RenodxDecision decision = RenodxPins.Decide(new(null, OwnFile: false, Driver61664, GraphicsApi.DX12, null));

        Assert.IsTrue(decision.IsPin);
        StringAssert.Contains(decision.Evidence, "D3D12Core.dll", StringComparison.Ordinal);
        Assert.IsNull(RenodxPins.Decide(new(null, OwnFile: false, null, GraphicsApi.DX12, null)).Evidence);
    }

    private async Task<PickedBuild> Pick(RenodxInputs inputs, RenodxReason reason)
    {
        RenodxDecision decision = RenodxPins.Decide(inputs);
        Assert.AreEqual(reason, decision.Reason);
        return RenodxPins.Pick(await MirrorBed.Captured(testContext), decision);
    }
}
