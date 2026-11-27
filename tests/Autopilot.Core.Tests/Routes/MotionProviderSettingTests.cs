using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Graphics;
using Autopilot.Core.Routes;

namespace Autopilot.Core.Tests.Routes;

/// <summary>The motion-vector provider a setting or an install record names, per routes.md's "What the person can override".</summary>
[TestClass]
public sealed class MotionProviderSettingTests
{
    [TestMethod]
    [DataRow(0, MotionProvider.Generic)]
    [DataRow(1, MotionProvider.Launchpad)]
    [DataRow(2, MotionProvider.Vort)]
    [DataRow(3, MotionProvider.LumeniteKernel)]
    [DataRow(5, MotionProvider.BuiltIn)]
    public void A_providers_number_is_that_provider(int number, MotionProvider provider) =>
        Assert.AreEqual(new MotionProviderSetting(provider, null), MotionProviderSetting.Read(number));

    [TestMethod]
    public void Provider_4_stops_the_install_and_says_why()
    {
        var setting = MotionProviderSetting.Read(4);

        Assert.IsNull(setting.Provider);
        Assert.AreEqual(
            "LumeniteFX renamed QuantMotion to PocketMV and the feeder does not read it yet; pick provider 3 or 5.",
            setting.Refusal?.Text());
    }

    [TestMethod]
    [DataRow(6, DisplayName = "a number no provider has")]
    [DataRow(-1, DisplayName = "a negative number")]
    [DataRow(null, DisplayName = "none, or a value of the wrong type")]
    public void Anything_else_reads_as_the_default(int? number)
    {
        var setting = MotionProviderSetting.Read(number);

        Assert.AreEqual(new MotionProviderSetting(null, null), setting);
        Assert.AreEqual(MotionProvider.LumeniteKernel, RouteComponents.ProviderFor(GraphicsApi.DX12, setting.Provider));
    }
}
