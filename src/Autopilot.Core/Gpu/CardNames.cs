using System.Globalization;
using System.Text.RegularExpressions;

namespace Autopilot.Core.Gpu;

/// <summary>What an NVIDIA card's name says about its architecture and its tensor cores.</summary>
internal static partial class CardNames
{
    /// <summary>The first architecture with tensor cores in consumer cards: RTX 20.</summary>
    public const int Turing = 75;

    /// <summary>RTX 40 (Ada Lovelace): the cards the multi-frame-generation unlock and OptiScaler's RTX 40 MFG package are for.</summary>
    public const int Rtx40 = 89;

    /// <summary>RTX 50 (Blackwell): the cards OptiScaler's author tested, and the only ones the routes steer to it.</summary>
    public const int Rtx50 = 120;

    // Every architecture the fatbin table names; 80, 87, 90, 100 and 121 are data-centre parts no card name reaches.
    private static readonly Dictionary<int, string> _families = new()
    {
        [75] = "RTX 20 / GTX 16 (Turing)",
        [80] = "A100 (Ampere DC)",
        [86] = "RTX 30 (Ampere)",
        [87] = "Orin",
        [89] = "RTX 40 (Ada Lovelace)",
        [90] = "H100 (Hopper)",
        [100] = "Blackwell (data centre)",
        [120] = "RTX 50 (Blackwell)",
        [121] = "Blackwell",
    };

    public static string? Family(int sm) => _families.GetValueOrDefault(sm);

    /// <summary>The card a name describes: its architecture and whether it has tensor cores, each null when the name does not say.</summary>
    public static GpuCard Read(string name)
    {
        string upper = name.ToUpperInvariant();
        if (NoTensorTuring().IsMatch(upper))
        {
            // GTX 16, MX 450/550 and the T-series workstation cards: Turing, which the runtime check needs, but no DLSS from the driver.
            return new GpuCard(name, Turing, HasTensorCores: false);
        }

        int? sm = Sm(upper);
        if (sm is null && OlderThanTuringNumber(upper))
        {
            return new GpuCard(name, null, HasTensorCores: false);
        }

        return new GpuCard(name, sm, sm is null ? null : sm >= Turing);
    }

    private static int? Sm(string upper)
    {
        if (!IsNvidiaName(upper))
        {
            return null;
        }

        // Workstation cards name the architecture, and their numbers are not GeForce series: "RTX 2000 Ada" is not an RTX 20.
        if (AdaWord().IsMatch(upper))
        {
            return 89;
        }

        if (upper.Contains("BLACKWELL", StringComparison.Ordinal))
        {
            return 120;
        }

        if (RtxA().IsMatch(upper))
        {
            return 86;
        }

        if (upper.Contains("QUADRO RTX", StringComparison.Ordinal) || upper.Contains("TITAN RTX", StringComparison.Ordinal))
        {
            return 75;
        }

        Match series = Series().Match(upper);
        return series.Success ? SeriesSm(int.Parse(series.Groups["number"].ValueSpan, CultureInfo.InvariantCulture)) : null;
    }

    private static int? SeriesSm(int number) => number switch
    {
        >= 5000 and <= 5999 => 120,
        >= 4000 and <= 4999 => 89,
        >= 3000 and <= 3999 => 86,
        (>= 2000 and <= 2999) or (>= 1600 and <= 1699) => 75,
        >= 1000 and <= 1099 => 61,
        _ => null,
    };

    // Changed from the reference, which left these unknown and so never refused them: a GTX or GT card numbered
    // below 1600 (GTX 980, GTX 750 Ti, GT 1030) is older than Turing, whatever its exact architecture.
    private static bool OlderThanTuringNumber(string upper)
    {
        Match match = GeForceNumber().Match(upper);
        return IsNvidiaName(upper)
            && match.Success
            && int.Parse(match.Groups["number"].ValueSpan, CultureInfo.InvariantCulture) < 1600;
    }

    /// <summary>Whether an upper-cased adapter name is NVIDIA's.</summary>
    internal static bool IsNvidiaName(string upper) =>
        upper.Contains("NVIDIA", StringComparison.Ordinal)
        || upper.Contains("GEFORCE", StringComparison.Ordinal)
        || upper.Contains("RTX", StringComparison.Ordinal)
        || upper.Contains("QUADRO", StringComparison.Ordinal);

    [GeneratedRegex(@"\bGTX\s*16\d{2}\b|\bMX\s*[45]50\b|(NVIDIA|QUADRO)\s+T(400|500|550|600|1000|1200|2000)\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex NoTensorTuring();

    [GeneratedRegex(@"\bADA\b", RegexOptions.CultureInvariant)]
    private static partial Regex AdaWord();

    [GeneratedRegex(@"RTX\s*A\d{3,4}\b", RegexOptions.CultureInvariant)]
    private static partial Regex RtxA();

    [GeneratedRegex(@"(RTX|GTX)\s*(?<number>\d{3,4})", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex Series();

    [GeneratedRegex(@"\bGTX?\s*(?<number>\d{3,4})\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex GeForceNumber();
}
