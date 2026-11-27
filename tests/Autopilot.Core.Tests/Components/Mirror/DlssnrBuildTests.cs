using Autopilot.Core.Components;
using Autopilot.Core.Components.Mirror;
using Autopilot.Core.Gpu;

namespace Autopilot.Core.Tests.Components.Mirror;

[TestClass]
public sealed class DlssnrBuildTests(TestContext testContext)
{
    // The mirror's list newest first, as captured.
    private static readonly string[] _mirrorOrder = ["310.9.16-nvidia", "310.9.16", "310.8.SF-v2", "310.8.0-RTX40", "310.8.SF", "310.8.Lecram", "310.8.0"];

    private static readonly GpuCard _rtx5090 = new("NVIDIA GeForce RTX 5090", 120, true);
    private static readonly GpuCard _rtx2070 = new("NVIDIA GeForce RTX 2070 SUPER", 75, true);

    [TestMethod]
    [DataRow(120, new[] { "310.9.16-nvidia", "310.8.0", "310.9.16", "310.8.SF-v2", "310.8.0-RTX40", "310.8.SF", "310.8.Lecram" })]
    [DataRow(121, new[] { "310.9.16-nvidia", "310.8.0", "310.9.16", "310.8.SF-v2", "310.8.0-RTX40", "310.8.SF", "310.8.Lecram" })]
    [DataRow(89, new[] { "310.8.0-RTX40", "310.9.16", "310.9.16-nvidia", "310.8.SF-v2", "310.8.SF", "310.8.Lecram", "310.8.0" })]
    [DataRow(86, new[] { "310.8.SF-v2", "310.8.SF", "310.9.16-nvidia", "310.9.16", "310.8.0-RTX40", "310.8.Lecram", "310.8.0" })]
    [DataRow(75, new[] { "310.8.SF-v2", "310.8.SF", "310.9.16-nvidia", "310.9.16", "310.8.0-RTX40", "310.8.Lecram", "310.8.0" })]
    public async Task The_build_made_for_the_card_is_tried_first(int sm, string[] order)
    {
        DlssnrCandidates candidates = DlssnrBuilds.Candidates(await MirrorBed.Captured(testContext), sm, chosenLabel: null);

        Assert.AreEqual(string.Join(", ", order), MirrorBed.Labels(candidates.Entries));
        Assert.IsFalse(candidates.Pinned);
    }

    [TestMethod]
    [DataRow(61, DisplayName = "below RTX 20")]
    [DataRow(null, DisplayName = "unknown card")]
    public async Task Without_a_preferred_build_the_mirrors_order_stands(int? sm)
    {
        DlssnrCandidates candidates = DlssnrBuilds.Candidates(await MirrorBed.Captured(testContext), sm, chosenLabel: null);

        Assert.AreEqual(string.Join(", ", _mirrorOrder), MirrorBed.Labels(candidates.Entries));
    }

    [TestMethod]
    public async Task A_chosen_build_is_the_only_candidate()
    {
        DlssnrCandidates candidates = DlssnrBuilds.Candidates(await MirrorBed.Captured(testContext), 89, "310.8.0");

        Assert.AreEqual("310.8.0", MirrorBed.Labels(candidates.Entries));
        Assert.IsTrue(candidates.Pinned);
        Assert.IsFalse(candidates.WantedMissing);
    }

    [TestMethod]
    public async Task A_chosen_build_not_on_the_list_is_the_newest_for_the_caller_to_say()
    {
        DlssnrCandidates candidates = DlssnrBuilds.Candidates(await MirrorBed.Captured(testContext), 89, "310.7.0");

        Assert.AreEqual("310.9.16-nvidia", MirrorBed.Labels(candidates.Entries));
        Assert.IsTrue(candidates.WantedMissing);
    }

    [TestMethod]
    public async Task An_empty_list_is_a_build_list_that_could_not_be_read()
    {
        BuildCatalog catalog = await MirrorBed.Captured(testContext, [.. _mirrorOrder.Select(label => "dlssnr-" + label)]);

        ComponentException failure = Assert.Throws<ComponentException>(() => DlssnrBuilds.Candidates(catalog, 89, null));

        Assert.AreEqual(ComponentFailure.BuildListUnavailable, failure.Reason);
    }

    [TestMethod]
    [DataRow(120, "310.8.0")]
    [DataRow(89, "310.8.0-RTX40")]
    [DataRow(86, "310.8.SF-v2")]
    [DataRow(75, "310.8.SF-v2")]
    public async Task On_auto_each_card_gets_its_build(int sm, string label)
    {
        DlssnrOutcome outcome = await Choose(new GpuCard("card", sm, true), chosenLabel: null);

        DlssnrChosen chosen = Assert.IsInstanceOfType<DlssnrChosen>(outcome);
        Assert.AreEqual(label, chosen.Entry.Label);
        Assert.AreEqual(DlssnrFit.Compatible, chosen.Check.Fit);
        Assert.IsEmpty(chosen.Skipped);
        Assert.IsEmpty(chosen.Untrusted);
        Assert.IsNull(chosen.Warning);
        Assert.IsNull(chosen.SkippedNote);
        Assert.IsNull(chosen.UntrustedNote);
    }

    [TestMethod]
    public async Task When_no_build_matches_every_label_tried_is_named()
    {
        DlssnrOutcome outcome = await Choose(DlssnrChoice.Gtx1080, chosenLabel: null);

        DlssnrRefused refused = Assert.IsInstanceOfType<DlssnrRefused>(outcome);
        Assert.AreEqual(
            "No suitable nvngx_dlssnr build found for NVIDIA GeForce GTX 1080 Ti. Tried: 310.8.SF-v2, 310.8.0-RTX40, 310.8.SF, 310.8.0. DLSS 5 currently runs on NVIDIA RTX 20 series and newer.",
            refused.Message);
        Assert.HasCount(4, refused.Tried);
    }

    [TestMethod]
    public async Task On_auto_a_build_that_does_not_match_is_skipped_and_named()
    {
        // A build the table has never heard of: its label says RTX 40, its file says otherwise.
        List<string> read = [];
        DlssnrOutcome outcome = await Choose(DlssnrChoice.Rtx4070, null, label =>
        {
            read.Add(label);
            return label == "310.8.0-RTX40" ? [120] : DlssnrChoice.RealBuild(label);
        });

        DlssnrChosen chosen = Assert.IsInstanceOfType<DlssnrChosen>(outcome);
        Assert.AreEqual("310.8.SF-v2", chosen.Entry.Label);
        Assert.AreEqual("skipped as incompatible: 310.8.0-RTX40", chosen.SkippedNote);
        Assert.AreEqual("310.8.0-RTX40, 310.8.SF-v2", string.Join(", ", read), "nothing is read after the match");
    }

    [TestMethod]
    public async Task A_chosen_build_that_does_not_match_stops_the_install()
    {
        DlssnrOutcome outcome = await Choose(DlssnrChoice.Rtx4070, "310.8.0");

        DlssnrRefused refused = Assert.IsInstanceOfType<DlssnrRefused>(outcome);
        Assert.AreEqual(
            "Build 310.8.0 will not run on NVIDIA GeForce RTX 4070. THIS BUILD WILL NOT RUN ON YOUR CARD. It contains no code for RTX 40 (Ada Lovelace). "
            + "File supports: RTX 50 (Blackwell). Leave the version on Auto and the tool picks the newest build that supports your card.",
            refused.Message);
    }

    [TestMethod]
    public async Task A_mismatch_the_person_overrode_is_placed_with_a_warning()
    {
        DlssnrOutcome outcome = await Choose(DlssnrChoice.Rtx3060, "310.8.0", placeMismatch: true);

        DlssnrChosen chosen = Assert.IsInstanceOfType<DlssnrChosen>(outcome);
        Assert.AreEqual(DlssnrFit.Incompatible, chosen.Check.Fit);
        Assert.AreEqual("dlssnr 310.8.0 does not match your card - installed anyway", chosen.Warning);
    }

    [TestMethod]
    public async Task With_no_card_detected_the_first_build_is_placed_with_a_warning()
    {
        DlssnrOutcome outcome = await Choose(card: null, chosenLabel: null);

        DlssnrChosen chosen = Assert.IsInstanceOfType<DlssnrChosen>(outcome);
        Assert.AreEqual("310.8.SF-v2", chosen.Entry.Label);
        Assert.AreEqual(
            "could not verify GPU compatibility (card not detected; file supports: RTX 20 / GTX 16 (Turing), RTX 30 (Ampere), RTX 40 (Ada Lovelace), RTX 50 (Blackwell))",
            chosen.Warning);
    }

    [TestMethod]
    public async Task A_file_with_no_readable_architecture_is_placed_with_a_warning()
    {
        DlssnrOutcome outcome = await Choose(_rtx5090, null, _ => []);

        DlssnrChosen chosen = Assert.IsInstanceOfType<DlssnrChosen>(outcome);
        Assert.AreEqual("310.8.0", chosen.Entry.Label);
        Assert.AreEqual(DlssnrFit.CannotTell, chosen.Check.Fit);
        Assert.AreEqual("could not verify GPU compatibility (no architectures could be read from the file)", chosen.Warning);
    }

    [TestMethod]
    public void A_compatible_file_lists_what_it_supports()
    {
        DlssnrCheck check = DlssnrBuilds.Check(new HashSet<int> { 120, 61, 86 }, DlssnrChoice.Rtx3060);

        Assert.AreEqual(DlssnrFit.Compatible, check.Fit);
        Assert.AreEqual("compatible with your card (file supports: sm_61, RTX 30 (Ampere), RTX 50 (Blackwell))", check.Message);
    }

    [TestMethod]
    public void The_check_reads_the_file_not_the_label()
    {
        string path = FatbinTests.WriteDll(FatbinTests.FatbinBytes(24, [75, 86]));
        try
        {
            DlssnrCheck check = DlssnrBuilds.Check(Fatbin.Architectures(path), _rtx2070);

            Assert.AreEqual(DlssnrFit.Compatible, check.Fit);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private Task<DlssnrOutcome> Choose(GpuCard? card, string? chosenLabel, Func<string, int[]>? architectures = null, bool placeMismatch = false) =>
        DlssnrChoice.ChooseAsync(testContext, card, chosenLabel, architectures ?? DlssnrChoice.RealBuild, placeMismatch);
}
