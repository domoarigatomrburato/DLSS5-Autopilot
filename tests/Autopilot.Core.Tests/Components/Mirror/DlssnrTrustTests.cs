using Autopilot.Core.Components.Mirror;
using Autopilot.Core.Gpu;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Components.Mirror;

/// <summary>
/// The candidates loop and the trust check of each build's file: on Auto an untrusted build is
/// skipped like an incompatible one; a chosen one stops the install, whatever the override.
/// </summary>
[TestClass]
public sealed class DlssnrTrustTests(TestContext testContext)
{
    [TestMethod]
    public async Task On_auto_a_build_whose_file_fails_its_trust_check_is_skipped_and_named()
    {
        List<string> read = [];
        DlssnrOutcome outcome = await Choose(DlssnrChoice.Rtx4070, null, label =>
        {
            read.Add(label);
            return label == "310.8.0-RTX40" ? throw Untrusted(VerificationProblem.DigestMismatch) : DlssnrChoice.RealBuild(label);
        });

        DlssnrChosen chosen = Assert.IsInstanceOfType<DlssnrChosen>(outcome);
        Assert.AreEqual("310.8.SF-v2", chosen.Entry.Label);
        Assert.AreEqual("skipped as untrusted: 310.8.0-RTX40", chosen.UntrustedNote);
        Assert.IsNull(chosen.SkippedNote, "an untrusted build is not called incompatible");
        Assert.AreEqual("310.8.0-RTX40, 310.8.SF-v2", string.Join(", ", read));
    }

    [TestMethod]
    public async Task When_every_build_is_untrusted_or_does_not_match_the_refusal_names_the_untrusted_ones_and_does_not_blame_the_card()
    {
        DlssnrOutcome outcome = await Choose(DlssnrChoice.Gtx1080, null, label => label == "310.8.0-RTX40" ? throw Untrusted(VerificationProblem.NotSigned) : DlssnrChoice.RealBuild(label));

        DlssnrRefused refused = Assert.IsInstanceOfType<DlssnrRefused>(outcome);
        Assert.AreEqual(
            "No nvngx_dlssnr build could be used for NVIDIA GeForce GTX 1080 Ti. Tried: 310.8.SF-v2, 310.8.0-RTX40, 310.8.SF, 310.8.0. These failed the trust check (NVIDIA's signature or the digest this release pinned): 310.8.0-RTX40. Update the tool or try again later.",
            refused.Message);
        Assert.HasCount(4, refused.Tried);
    }

    [TestMethod]
    public async Task With_no_card_detected_and_every_build_untrusted_the_refusal_names_them_all()
    {
        DlssnrOutcome outcome = await Choose(card: null, chosenLabel: null, _ => throw Untrusted(VerificationProblem.DigestMismatch));

        DlssnrRefused refused = Assert.IsInstanceOfType<DlssnrRefused>(outcome);
        Assert.AreEqual(
            "No nvngx_dlssnr build could be used for your card. Tried: 310.8.SF-v2, 310.8.0-RTX40, 310.8.SF, 310.8.0. These failed the trust check (NVIDIA's signature or the digest this release pinned): 310.8.SF-v2, 310.8.0-RTX40, 310.8.SF, 310.8.0. Update the tool or try again later.",
            refused.Message);
    }

    [TestMethod]
    public async Task A_chosen_pinned_build_that_does_not_match_its_digest_stops_the_install()
    {
        DlssnrOutcome outcome = await Choose(DlssnrChoice.Rtx3060, "310.8.SF", _ => throw Untrusted(VerificationProblem.DigestMismatch), placeMismatch: true);

        DlssnrRefused refused = Assert.IsInstanceOfType<DlssnrRefused>(outcome);
        Assert.AreEqual(
            "The nvngx_dlssnr.dll in build 310.8.SF does not match the digest this release pinned; pick another build or update the tool.",
            refused.Message);
        Assert.AreEqual("310.8.SF", MirrorBed.Labels(refused.Tried));
    }

    [TestMethod]
    public async Task A_chosen_build_with_no_pin_and_no_nvidia_signature_stops_the_install()
    {
        DlssnrOutcome outcome = await DlssnrBuilds.ChooseAsync(
            new([DlssnrChoice.UnpinnedCommunityBuild], Pinned: true, WantedMissing: false),
            DlssnrChoice.Rtx3060,
            placeMismatch: true,
            (_, _) => throw Untrusted(VerificationProblem.NotSigned),
            testContext.CancellationToken);

        DlssnrRefused refused = Assert.IsInstanceOfType<DlssnrRefused>(outcome);
        Assert.AreEqual("310.10.SF carries neither NVIDIA's signature nor a digest this release pinned; pick another build or update the tool.", refused.Message);
    }

    [TestMethod]
    public async Task A_read_that_fails_for_another_reason_ends_the_loop()
    {
        NetException timeout = new(NetErrorKind.Timeout, "github.com", "timed out");
        NetException wrong = new(NetErrorKind.WrongContent, "github.com", "not a zip") { Detail = new WrongContentDetail("a.zip", "<html") };

        Assert.AreSame(timeout, await Assert.ThrowsAsync<NetException>(() => Choose(DlssnrChoice.Rtx4070, null, _ => throw timeout)));
        Assert.AreSame(wrong, await Assert.ThrowsAsync<NetException>(() => Choose(DlssnrChoice.Rtx4070, null, _ => throw wrong)));
    }

    // What the read callback throws when the file taken out of the zip fails its contents check.
    private static NetException Untrusted(VerificationProblem problem) =>
        new(NetErrorKind.VerificationFailed, "github.com", "nvngx_dlssnr.dll: failed its check") { Detail = new VerificationFailedDetail(problem, "nvngx_dlssnr.dll") };

    private Task<DlssnrOutcome> Choose(GpuCard? card, string? chosenLabel, Func<string, int[]> architectures, bool placeMismatch = false) =>
        DlssnrChoice.ChooseAsync(testContext, card, chosenLabel, architectures, placeMismatch);
}
