using Autopilot.Core.Components.Mirror;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Components.Mirror;

/// <summary>The <c>nvngx_dlssnr.dll</c> trust rule: NVIDIA's signature, or the digest pinned for a community build's label.</summary>
[TestClass]
public sealed class DlssnrPinTests(TestContext testContext)
{
    private static readonly string[] _pinned = ["310.8.0-RTX40", "310.8.SF", "310.8.SF-v2", "310.8.Lecram", "310.9.16"];

    [TestMethod]
    public async Task Every_community_build_on_the_mirror_is_pinned_and_nvidias_own_are_not()
    {
        BuildCatalog catalog = await MirrorBed.Captured(testContext);

        string[] labels = [.. catalog.Entries(BuildFamily.Dlssnr).Select(entry => entry.Label)];

        CollectionAssert.AreEquivalent(_pinned, DlssnrPins.Table.Select(pin => pin.Label).ToArray());
        CollectionAssert.IsSubsetOf(DlssnrPins.Table.Select(pin => pin.Label).ToArray(), labels);
        Assert.IsNull(DlssnrPins.Of("310.8.0"));
        Assert.IsNull(DlssnrPins.Of("310.9.16-nvidia"));
    }

    [TestMethod]
    public void A_community_build_preferred_for_a_card_carries_code_for_it()
    {
        foreach ((int minSm, _, string[] labels) in DlssnrPins.Preferred)
        {
            foreach (DlssnrPin pin in labels.Select(DlssnrPins.Of).OfType<DlssnrPin>())
            {
                Assert.Contains(minSm, pin.Sm, $"{pin.Label} for sm {minSm}");
            }
        }
    }

    [TestMethod]
    public void A_build_this_release_has_no_pin_for_must_carry_nvidias_signature() =>
        Assert.AreEqual(new SignerVerification("NVIDIA Corporation"), DlssnrChoice.UnpinnedCommunityBuild.Contents);
}
