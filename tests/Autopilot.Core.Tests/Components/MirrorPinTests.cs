using System.Net;
using Autopilot.Core.Components;
using Autopilot.Core.Graphics;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Components;

/// <summary>The pinned builds on the API's path: the list stops at 100 releases, and the pins are old builds.</summary>
[TestClass]
public sealed class MirrorPinTests(TestContext testContext)
{
    private static readonly RenodxDecision _driverFault = RenodxPins.Decide(new(null, OwnFile: false, "616.64", GraphicsApi.DX12, null));

    [TestMethod]
    public async Task A_pinned_build_past_the_first_100_releases_is_asked_by_its_tag()
    {
        using MirrorBed bed = new(testContext);
        bed.NvidiaRedirects().ApiHasTag("renodx-dlss5-4.55").ApiHasTag("renodx-dlss5-4.60").Server.Answer(MirrorBed.Api, HttpStatusCode.OK, MirrorFixtures.ApiListPastPins());

        BuildCatalog catalog = await bed.Get();
        _ = await bed.Get();

        Assert.AreEqual("10.8.0, 8.5.0-rc10, 8.5.0-rc5, 8.5.0-rc4, 7.0.0-rc8, 7.0.0-rc1, 6.5.3, 6.4.1, 5.2.1, 4.70, 4.60, 4.55, 4.5, 3.3.4, 3.3.1, 2.5", MirrorBed.Labels(catalog, BuildFamily.Renodx));
        Assert.AreEqual(1, bed.RequestsTo(MirrorBed.ApiTag("renodx-dlss5-4.55")), "asked once, then remembered with the list");
        Assert.AreEqual(1, bed.RequestsTo(MirrorBed.ApiTag("renodx-dlss5-4.60")));
        Assert.AreEqual(new Sha256Verification(MirrorFixtures.Renodx455Digest), catalog.Entries(BuildFamily.Renodx).Single(entry => entry.Label == "4.55").Download);
    }

    [TestMethod]
    public async Task A_pinned_build_on_the_list_is_not_asked_again()
    {
        using MirrorBed bed = new(testContext);
        _ = await bed.ApiAnswers().NvidiaRedirects().Get();

        Assert.IsFalse(bed.Server.Requests.Any(request => request.Url.AbsolutePath.Contains("/releases/tags/", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task A_pin_whose_tag_the_api_does_not_know_stays_missing()
    {
        using MirrorBed bed = new(testContext);
        bed.NvidiaRedirects().ApiLacksTag("renodx-dlss5-4.55").ApiHasTag("renodx-dlss5-4.60").Server.Answer(MirrorBed.Api, HttpStatusCode.OK, MirrorFixtures.ApiListPastPins());

        BuildCatalog catalog = await bed.Get();

        StringAssert.Contains(MirrorBed.Labels(catalog, BuildFamily.Renodx), "4.70, 4.60, 4.5,", StringComparison.Ordinal);
        Assert.AreEqual("4.55", Assert.Throws<RenodxPinMissingException>(() => RenodxPins.Pick(catalog, _driverFault)).Label);
    }

    [TestMethod]
    public async Task A_pin_the_network_kept_out_is_asked_again_by_the_next_install()
    {
        using MirrorBed bed = new(testContext);
        bed.NvidiaRedirects().ApiHasTag("renodx-dlss5-4.60").Unreachable(MirrorBed.ApiTag("renodx-dlss5-4.55")).ApiHasTag("renodx-dlss5-4.55")
            .Unreachable(MirrorBed.Expanded("renodx-dlss5-4.55"))
            .Server.Answer(MirrorBed.Api, HttpStatusCode.OK, MirrorFixtures.ApiListPastPins());

        BuildCatalog first = await bed.Get();
        BuildCatalog second = await bed.Get();

        Assert.IsFalse(first.Entries(BuildFamily.Renodx).Any(entry => entry.Label == "4.55"));
        Assert.IsTrue(second.Entries(BuildFamily.Renodx).Any(entry => entry.Label == "4.55"));
        Assert.AreEqual(2, bed.RequestsTo(MirrorBed.ApiTag("renodx-dlss5-4.55")));
    }
}
