using Autopilot.Core.Gpu;

namespace Autopilot.Core.Tests.Gpu;

/// <summary>Readings as Windows would report them, for the facts to be drawn from.</summary>
internal static class GpuReadings
{
    public const int Nvidia = 0x10DE;
    public const int Intel = 0x8086;
    public const int Amd = 0x1002;
    public const int AmdAlt = 0x1022;
    public const int MicrosoftVendor = 0x1414;

    public static GraphicsAdapter Card(string name) => new(Nvidia, name, Software: false);

    public static GraphicsAdapter Adapter(int vendor, string name) => new(vendor, name, Software: false);

    public static GpuFacts Facts(params GraphicsAdapter[] adapters) => GpuFacts.From(new GpuReading(adapters, "32.0.16.1692", []));

    public static GpuFacts Driver(string? nvapi, params DisplayClassEntry[] displayClass) =>
        GpuFacts.From(new GpuReading([Card("NVIDIA GeForce RTX 4070")], nvapi, displayClass));
}
