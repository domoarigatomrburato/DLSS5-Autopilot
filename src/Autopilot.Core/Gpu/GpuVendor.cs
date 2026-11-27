namespace Autopilot.Core.Gpu;

/// <summary>Who made the graphics hardware that decides what can run.</summary>
public enum GpuVendor
{
    /// <summary>No NVIDIA, AMD or Intel adapter was found.</summary>
    None,

    /// <summary>At least one NVIDIA adapter.</summary>
    Nvidia,

    /// <summary>No NVIDIA adapter, and at least one AMD one.</summary>
    Amd,

    /// <summary>No NVIDIA or AMD adapter, and at least one Intel one.</summary>
    Intel,
}
