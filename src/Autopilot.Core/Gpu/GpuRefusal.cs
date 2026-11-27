using Autopilot.Core.Resources;

namespace Autopilot.Core.Gpu;

/// <summary>Why no route can run DLSS 5 on this PC. It only says what applies: the caller decides whether the person installs anyway.</summary>
public abstract record GpuRefusal
{
    private protected GpuRefusal()
    {
    }

    /// <summary>The refusal in words.</summary>
    public abstract string Explain();
}

/// <summary>A Turing card without tensor cores (GTX 16, MX 450/550, T400 to T2000): the driver offers no DLSS on it.</summary>
/// <param name="Card">The card's name.</param>
public sealed record NoTensorCores(string Card) : GpuRefusal
{
    public override string Explain() => Texts.Format("GpuNoTensorCores", Card) + " " + Texts.Text("GpuNeedsRtx");
}

/// <summary>A card older than Turing (RTX 20): it has no tensor cores.</summary>
/// <param name="Card">The card's name.</param>
public sealed record OlderThanRtx20(string Card) : GpuRefusal
{
    public override string Explain() => Texts.Format("GpuOlderThanRtx20", Card) + " " + Texts.Text("GpuNeedsRtx");
}

/// <summary>An AMD or Intel adapter and no NVIDIA one: there is no NVIDIA runtime for DLSS 5 to use.</summary>
/// <param name="Vendor">The vendor found instead, <see cref="GpuVendor.Amd"/> or <see cref="GpuVendor.Intel"/>.</param>
public sealed record NoNvidiaCard(GpuVendor Vendor) : GpuRefusal
{
    /// <summary>The headline, then what the vendor found can and cannot do.</summary>
    public override string Explain() =>
        Texts.Text("GpuNoNvidiaCard") + "\n" + Texts.Text(Vendor == GpuVendor.Amd ? "GpuAmdAnswer" : "GpuIntelAnswer");
}
