using Autopilot.Core.Components;

namespace Autopilot.Core.Tests.Components;

[TestClass]
public sealed class FeederPinTests
{
    [TestMethod]
    [DataRow("v0.13.0", FeederNeed.Direct3D10, "DLSS5-Feeder v0.13.0 refuses Direct3D 10 games; support arrived in v0.13.1-beta.1. Pick v0.13.1-beta.1 or newer under 'feeder build'.")]
    [DataRow("v1.17.0", FeederNeed.HelperProcess, "DLSS5-Feeder v1.17.0 has no 64-bit helper-process mode; it arrived in v1.18.0-beta.1. Pick v1.18.0-beta.1 or newer under 'feeder build'.")]
    [DataRow("v1.19.1-beta.1", FeederNeed.BuiltInProvider, "DLSS5-Feeder v1.19.1-beta.1 has no built-in motion-vector provider; it arrived in v2.0.0. Pick v2.0.0 or newer under 'feeder build', or another provider.")]
    public void A_build_older_than_a_needed_pin_is_refused_before_anything_is_written(string feeder, FeederNeed need, string message)
    {
        FeederPinCheck check = FeederPins.Check(new(feeder), [need]);

        Assert.IsFalse(check.Allowed);
        Assert.AreEqual(message, Assert.ContainsSingle(check.Refusals));
        Assert.IsEmpty(check.Warnings);
    }

    [TestMethod]
    [DataRow("v0.13.1-beta.1", FeederNeed.Direct3D10)]
    [DataRow("v1.18.0-beta.1", FeederNeed.HelperProcess)]
    [DataRow("v2.0.0", FeederNeed.BuiltInProvider)]
    [DataRow("v0.15.1", FeederNeed.HdrDisplay)]
    [DataRow("v0.16.0-beta.1", FeederNeed.HdrDisplay)]
    public void The_pinned_build_itself_and_newer_ones_pass(string feeder, FeederNeed need)
    {
        FeederPinCheck check = FeederPins.Check(new(feeder), [need]);

        Assert.IsTrue(check.Allowed);
        Assert.IsEmpty(check.Warnings);
    }

    [TestMethod]
    public void A_build_older_than_the_hdr_pin_is_installed_with_a_warning()
    {
        FeederPinCheck check = FeederPins.Check(new("v0.15.1-beta.2"), [FeederNeed.HdrDisplay]);

        Assert.IsTrue(check.Allowed);
        Assert.AreEqual(
            "DLSS5-Feeder v0.15.1-beta.2 blows out HDR highlights; v0.15.1 fixed that. Pick v0.15.1 or newer under 'feeder build' for an HDR display.",
            Assert.ContainsSingle(check.Warnings));
    }

    [TestMethod]
    public void Only_the_needs_the_install_has_are_checked()
    {
        FeederPinCheck check = FeederPins.Check(new("v0.12.0"), [FeederNeed.HdrDisplay, FeederNeed.Direct3D10]);

        Assert.HasCount(1, check.Refusals, "the helper and provider pins are not this install's");
        Assert.HasCount(1, check.Warnings);
    }

    [TestMethod]
    public void A_version_that_cannot_be_compared_is_never_called_older() =>
        Assert.IsTrue(FeederPins.Check(new("latest"), [FeederNeed.Direct3D10, FeederNeed.BuiltInProvider]).Allowed);
}
