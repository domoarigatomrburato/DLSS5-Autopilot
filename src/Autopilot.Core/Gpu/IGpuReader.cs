namespace Autopilot.Core.Gpu;

/// <summary>Reads what Windows reports about the graphics adapters and the NVIDIA driver; <see cref="GpuFacts.From"/> turns it into facts.</summary>
public interface IGpuReader
{
    /// <summary>The adapters, the driver's file version and the display-class entries, as read once for this run.</summary>
    GpuReading Read();
}
