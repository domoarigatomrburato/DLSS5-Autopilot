namespace Autopilot.Core.Gpu;

/// <summary>The graphics card and the driver, as facts the routes decide on.</summary>
public sealed record GpuFacts
{
    private const int NvidiaVendor = 0x10DE;
    private const int IntelVendor = 0x8086;
    private const int AmdVendor = 0x1002;
    private const int AmdAltVendor = 0x1022;

    /// <summary>The NVIDIA card the tool works for: the newest architecture, then the one with tensor cores; null without an NVIDIA adapter.</summary>
    public GpuCard? Card { get; private init; }

    /// <summary>Whose hardware decides what can run.</summary>
    public GpuVendor Vendor { get; private init; }

    /// <summary>The driver's version as people know it ("616.92"); null when it cannot be read.</summary>
    public string? Driver { get; private init; }

    /// <summary>Windows has an NVIDIA card and an Intel or AMD one, so the graphics preference picks between them; two NVIDIA cards are not.</summary>
    public bool TwoGpus { get; private init; }

    /// <summary>Why no route can run DLSS 5 here; null when nothing is refused, which includes every case the reading cannot tell.</summary>
    public GpuRefusal? Refusal { get; private init; }

    /// <summary>The facts in a reading. Software adapters never count.</summary>
    public static GpuFacts From(GpuReading reading)
    {
        GraphicsAdapter[] hardware = [.. reading.Adapters.Where(adapter => !adapter.Software)];
        GpuCard[] nvidia = [.. hardware.Where(adapter => adapter.VendorId == NvidiaVendor).Select(adapter => CardNames.Read(adapter.Description))];
        GpuVendor vendor = VendorOf(hardware, nvidia.Length > 0);
        GpuCard? card = nvidia
            .OrderByDescending(candidate => candidate.Sm ?? -1)
            .ThenByDescending(candidate => candidate.HasTensorCores == true)
            .FirstOrDefault();
        return new GpuFacts
        {
            Card = card,
            Vendor = vendor,
            Driver = NvidiaDriver.Version(reading),
            Refusal = RefusalOf(vendor, card, nvidia),
            TwoGpus = nvidia.Length > 0 && hardware.Any(adapter => adapter.VendorId is IntelVendor or AmdVendor or AmdAltVendor),
        };
    }

    private static GpuVendor VendorOf(GraphicsAdapter[] hardware, bool anyNvidia)
    {
        if (anyNvidia)
        {
            return GpuVendor.Nvidia;
        }

        if (hardware.Any(adapter => adapter.VendorId is AmdVendor or AmdAltVendor))
        {
            return GpuVendor.Amd;
        }

        return hardware.Any(adapter => adapter.VendorId == IntelVendor) ? GpuVendor.Intel : GpuVendor.None;
    }

    private static GpuRefusal? RefusalOf(GpuVendor vendor, GpuCard? card, GpuCard[] nvidia)
    {
        if (vendor is GpuVendor.Amd or GpuVendor.Intel)
        {
            return new NoNvidiaCard(vendor);
        }

        // A name the table cannot read may be a card newer than the table: while one is present, nothing is refused.
        if (card is null || nvidia.Any(candidate => candidate.HasTensorCores is null))
        {
            return null;
        }

        if (card.OlderThanTuring)
        {
            return new OlderThanRtx20(card.Name);
        }

        return card.HasTensorCores == false ? new NoTensorCores(card.Name) : null;
    }
}
