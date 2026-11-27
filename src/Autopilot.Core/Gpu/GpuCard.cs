namespace Autopilot.Core.Gpu;

/// <summary>The NVIDIA card the tool works for.</summary>
/// <param name="Name">The adapter's name.</param>
/// <param name="Sm">Its CUDA architecture (75, 86, 89, 120, 61 ...); null when the name does not say it.</param>
/// <param name="HasTensorCores">Whether it has tensor cores; null when the name does not say.</param>
public sealed record GpuCard(string Name, int? Sm, bool? HasTensorCores)
{
    /// <summary>The card family people know ("RTX 40 (Ada Lovelace)"); null for an architecture without one.</summary>
    public string? Family => Sm is { } sm ? CardNames.Family(sm) : null;

    /// <summary>Older than Turing: an architecture below 75, or a GTX or GT card numbered below 1600.</summary>
    internal bool OlderThanTuring => Sm < CardNames.Turing || (Sm is null && HasTensorCores == false);
}
