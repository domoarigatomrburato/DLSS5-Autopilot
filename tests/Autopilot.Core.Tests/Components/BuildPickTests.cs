using Autopilot.Core.Components;

namespace Autopilot.Core.Tests.Components;

[TestClass]
public sealed class BuildPickTests(TestContext testContext)
{
    [TestMethod]
    [DataRow("4.60", "4.60", DisplayName = "by label")]
    [DataRow("renodx-dlss5-4.55", "4.55", DisplayName = "by tag")]
    [DataRow(null, "10.8.0", DisplayName = "nothing wanted: the newest")]
    public async Task The_wanted_build_is_found_by_label_or_tag(string? wanted, string label)
    {
        BuildCatalog catalog = await MirrorBed.Captured(testContext);

        PickedBuild picked = catalog.Pick(BuildFamily.Renodx, wanted);

        Assert.AreEqual(label, picked.Entry.Label);
        Assert.IsFalse(picked.WantedMissing);
    }

    [TestMethod]
    public async Task A_wanted_build_not_on_the_list_gives_the_newest_and_says_so()
    {
        BuildCatalog catalog = await MirrorBed.Captured(testContext);

        PickedBuild picked = catalog.Pick(BuildFamily.Dlssnr, "310.7.0");

        Assert.AreEqual("310.9.16-nvidia", picked.Entry.Label);
        Assert.IsTrue(picked.WantedMissing);
    }

    [TestMethod]
    public async Task Nvidias_own_runtime_is_the_newest_of_its_family()
    {
        BuildCatalog catalog = await MirrorBed.Captured(testContext);

        Assert.AreEqual(BuildPublisher.Nvidia, catalog.Pick(BuildFamily.Dlssg, null).Entry.Publisher);
        Assert.AreEqual("v310.9.16", catalog.Pick(BuildFamily.Dlss, "310.9.16 (NVIDIA SDK)").Entry.Tag);
    }

    [TestMethod]
    public async Task An_empty_family_is_a_build_list_that_could_not_be_read()
    {
        using MirrorBed bed = new(testContext);
        BuildCatalog catalog = await bed.ApiAnswers().Unreachable(MirrorBed.NvidiaLatest).Get();

        ComponentException failure = Assert.Throws<ComponentException>(() => catalog.Pick(BuildFamily.Dlssd, null));

        Assert.AreEqual(ComponentFailure.BuildListUnavailable, failure.Reason);
        Assert.AreEqual(BuildFamily.Dlssd, failure.Family);
        Assert.AreEqual(
            "The nvngx_dlssd.dll build list could not be read: GitHub could not be reached and nothing is cached yet. Try again in a few minutes.",
            failure.Message);
    }

    [TestMethod]
    public async Task Placing_tries_the_chosen_build_then_the_next_one()
    {
        BuildCatalog catalog = await MirrorBed.Captured(testContext);

        Assert.AreEqual("4.60, 4.55", MirrorBed.Labels(catalog.Candidates(catalog.Pick(BuildFamily.Renodx, "4.60"))));
        Assert.AreEqual("310.9.16 (NVIDIA SDK), 310.9.16", MirrorBed.Labels(catalog.Candidates(catalog.Pick(BuildFamily.Dlss, null))));
    }

    [TestMethod]
    public async Task The_last_build_of_a_family_is_tried_alone()
    {
        BuildCatalog catalog = await MirrorBed.Captured(testContext);

        Assert.ContainsSingle(catalog.Candidates(catalog.Pick(BuildFamily.Renodx, "2.5")));
        Assert.ContainsSingle(catalog.Candidates(catalog.Pick(BuildFamily.Dlssd, null)));
    }
}
