namespace Autopilot.Core.Gpu;

/// <summary>What Windows reports about the graphics hardware, before any of it is interpreted.</summary>
/// <param name="Adapters">The present, enabled adapters, software ones included and flagged.</param>
/// <param name="NvapiFileVersion">The file version of System32's <c>nvapi64.dll</c> ("32.0.16.1692"); null when it cannot be read.</param>
/// <param name="DisplayClass">The display-class entries in the registry, read for the driver only when <paramref name="NvapiFileVersion"/> is null.</param>
public sealed record GpuReading(
    IReadOnlyList<GraphicsAdapter> Adapters,
    string? NvapiFileVersion,
    IReadOnlyList<DisplayClassEntry> DisplayClass);

/// <summary>One graphics adapter as Windows lists it.</summary>
/// <param name="VendorId">The PCI vendor id: <c>0x10DE</c> NVIDIA, <c>0x8086</c> Intel, <c>0x1002</c> or <c>0x1022</c> AMD.</param>
/// <param name="Description">The adapter's name ("NVIDIA GeForce RTX 4070").</param>
/// <param name="Software">Whether Windows flags it as a software adapter, which never counts.</param>
public sealed record GraphicsAdapter(int VendorId, string Description, bool Software);

/// <summary>One entry of the registry's display class: the card it was made for and its driver.</summary>
/// <param name="Description">The entry's <c>DriverDesc</c>.</param>
/// <param name="DriverVersion">The entry's <c>DriverVersion</c> ("32.0.16.1692"); null when absent.</param>
/// <remarks>The registry keeps an entry for every card and driver the PC ever had, so these entries may be stale.</remarks>
public sealed record DisplayClassEntry(string Description, string? DriverVersion);
