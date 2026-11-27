using Autopilot.Core.Components.Mirror;
using Autopilot.Core.Gpu;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Components.Mirror;

/// <summary>The candidates loop over the captured mirror, with the builds whose architectures are known.</summary>
internal static class DlssnrChoice
{
    // The builds whose fatbins have been read, with the architectures each carries code for (the reference's gpu.py module comment).
    private static readonly Dictionary<string, int[]> _realBuilds = new()
    {
        ["310.8.0"] = [120],
        ["310.8.0-RTX40"] = [89, 120],
        ["310.8.SF"] = [75, 86, 89, 120],
        ["310.8.SF-v2"] = [75, 86, 89, 120],
    };

    public static GpuCard Rtx4070 { get; } = new("NVIDIA GeForce RTX 4070", 89, true);

    public static GpuCard Rtx3060 { get; } = new("NVIDIA GeForce RTX 3060", 86, true);

    public static GpuCard Gtx1080 { get; } = new("NVIDIA GeForce GTX 1080 Ti", 61, false);

    /// <summary>A community build published after this release: no pin names it.</summary>
    public static BuildEntry UnpinnedCommunityBuild { get; } = new(
        BuildFamily.Dlssnr,
        BuildPublisher.Mirror,
        "dlssnr-310.10.SF",
        "310.10.SF",
        new("https://github.com/RankFTW/rhi-repo/releases/download/dlssnr-310.10.SF/nvngx_dlssnr_310.10.SF.zip"),
        "nvngx_dlssnr_310.10.SF.zip",
        new NoVerification("a test build"));

    /// <summary>The architectures the build labelled <paramref name="label"/> carries code for.</summary>
    public static int[] RealBuild(string label) => _realBuilds[label];

    /// <summary>
    /// Chooses among the captured builds whose architectures are known, the read of each answering
    /// <paramref name="architectures"/> (or throwing what it throws).
    /// </summary>
    public static async Task<DlssnrOutcome> ChooseAsync(TestContext context, GpuCard? card, string? chosenLabel, Func<string, int[]> architectures, bool placeMismatch)
    {
        BuildCatalog catalog = await MirrorBed.Captured(context, "dlssnr-310.9.16-nvidia", "dlssnr-310.9.16", "dlssnr-310.8.Lecram");
        DlssnrCandidates candidates = DlssnrBuilds.Candidates(catalog, card?.Sm, chosenLabel);
        return await DlssnrBuilds.ChooseAsync(
            candidates,
            card,
            placeMismatch,
            (entry, _) => Task.FromResult<IReadOnlySet<int>>(architectures(entry.Label).ToHashSet()),
            context.CancellationToken);
    }
}
