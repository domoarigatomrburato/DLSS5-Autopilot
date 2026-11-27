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
    public bool TwoGpus => OtherGpu is not null;

    /// <summary>
    /// The other GPU beside an NVIDIA card (<see cref="GpuVendor.Intel"/> or <see cref="GpuVendor.Amd"/>), on which
    /// Windows may run a game where NGX does not exist; null on a PC without both. From the hardware adapters, or
    /// from the display class's names when Windows listed no adapter.
    /// </summary>
    public GpuVendor? OtherGpu { get; private init; }

    /// <summary>Why no route can run DLSS 5 here; null when nothing is refused, which includes every case the reading cannot tell.</summary>
    public GpuRefusal? Refusal { get; private init; }

    /// <summary>The facts in a reading. Software adapters never count.</summary>
    public static GpuFacts From(GpuReading reading)
    {
        GraphicsAdapter[] hardware = [.. reading.Adapters.Where(adapter => !adapter.Software)];
        GpuCard[] nvidia = [.. hardware.Where(adapter => adapter.VendorId == NvidiaVendor).Select(adapter => CardNames.Read(adapter.Description))];
        GpuVendor[] vendors = [.. hardware.Select(adapter => VendorOfId(adapter.VendorId))];
        GpuVendor vendor = VendorOf(vendors);
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
            OtherGpu = reading.Adapters.Count > 0
                ? OtherOf(vendors)
                : OtherOf([.. reading.DisplayClass.Select(entry => VendorOfName(entry.Description))]),
        };
    }

    // The other GPU of a pair: Intel or AMD, when an NVIDIA one is there too.
    private static GpuVendor? OtherOf(GpuVendor[] vendors) =>
        vendors.Contains(GpuVendor.Nvidia) ? vendors.Cast<GpuVendor?>().FirstOrDefault(vendor => vendor is GpuVendor.Intel or GpuVendor.Amd) : null;

    private static GpuVendor VendorOfId(int id) => id switch
    {
        NvidiaVendor => GpuVendor.Nvidia,
        AmdVendor or AmdAltVendor => GpuVendor.Amd,
        IntelVendor => GpuVendor.Intel,
        _ => GpuVendor.None,
    };

    // The registry's names, when DXGI could not be asked: the card table's NVIDIA names, then Intel's and AMD's.
    private static GpuVendor VendorOfName(string name)
    {
        string upper = name.ToUpperInvariant();
        if (CardNames.IsNvidiaName(upper))
        {
            return GpuVendor.Nvidia;
        }

        if (upper.Contains("INTEL", StringComparison.Ordinal))
        {
            return GpuVendor.Intel;
        }

        return upper.Contains("RADEON", StringComparison.Ordinal) || upper.Contains("AMD ", StringComparison.Ordinal) || upper.Contains("ADVANCED MICRO", StringComparison.Ordinal)
            ? GpuVendor.Amd
            : GpuVendor.None;
    }

    // Whose hardware decides: NVIDIA's whenever one is there, then AMD's, then Intel's.
    private static GpuVendor VendorOf(GpuVendor[] vendors) =>
        new[] { GpuVendor.Nvidia, GpuVendor.Amd, GpuVendor.Intel }.FirstOrDefault(vendors.Contains, GpuVendor.None);

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
