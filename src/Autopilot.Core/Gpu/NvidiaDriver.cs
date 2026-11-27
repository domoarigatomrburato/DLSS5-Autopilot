using System.Globalization;

namespace Autopilot.Core.Gpu;

/// <summary>The NVIDIA driver's version as people know it ("616.92"), and the versions the routes decide on.</summary>
public static class NvidiaDriver
{
    /// <summary>
    /// The first driver documented to carry the neural-rendering runtime: on anything older the install finishes and the
    /// model never runs.
    /// </summary>
    public const string FirstWithNeuralRuntime = "616.56";

    /// <summary>
    /// The first driver on which <c>renodx-dlss5</c> 4.6 and 4.7 fault inside <c>D3D12Core.dll</c> on every evaluate
    /// (616.86 and the later builds in the reports behave the same), so the add-on is pinned to 4.55 from here on.
    /// </summary>
    public const string FirstWithRenodxFault = "616.64";

    /// <summary>Whether <paramref name="driver"/> is <paramref name="version"/> or newer, compared as dotted integers.</summary>
    /// <param name="driver">The driver's version ("616.92"), or null when it is not known.</param>
    /// <param name="version">The version to compare with.</param>
    /// <returns>Null when either side is missing or not dotted integers: an unknown answer never warns or refuses.</returns>
    public static bool? AtLeast(string? driver, string version) =>
        Parse(driver) is { } have && Parse(version) is { } want ? Compare(have, want) >= 0 : null;

    /// <summary>The driver installed now: <c>nvapi64.dll</c>'s version, else the newest NVIDIA entry of the display class.</summary>
    internal static string? Version(GpuReading reading)
    {
        if (reading.NvapiFileVersion is { } fileVersion)
        {
            return Marketing(fileVersion);
        }

        // The registry keeps an entry for every driver the PC ever had: the newest NVIDIA one is the installed one.
        return reading.DisplayClass
            .Where(entry => entry.DriverVersion is not null && entry.Description.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
            .Select(entry => Marketing(entry.DriverVersion!))
            .Select(version => (Version: version, Parts: Parse(version)))
            .Where(candidate => candidate.Parts is not null)
            .OrderByDescending(candidate => candidate.Parts!, Comparer<int[]>.Create(Compare))
            .Select(candidate => candidate.Version)
            .FirstOrDefault();
    }

    /// <summary>"32.0.16.1692" becomes "616.92": the third part's last digit and the fourth part, split before the last two digits; anything else comes back as given.</summary>
    internal static string Marketing(string fileVersion)
    {
        string[] parts = fileVersion.Split('.');
        if (parts.Length != 4 || parts[2].Length == 0 || parts[3].Length == 0 || !parts[3].All(char.IsAsciiDigit))
        {
            return fileVersion;
        }

        string digits = parts[2][^1] + parts[3];
        return digits.Length >= 5 ? $"{digits[..^2]}.{digits[^2..]}" : fileVersion;
    }

    private static int[]? Parse(string? version)
    {
        if (string.IsNullOrEmpty(version))
        {
            return null;
        }

        string[] parts = version.Split('.');
        int[] numbers = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0 || !parts[i].All(char.IsAsciiDigit)
                || !int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
            {
                return null;
            }
        }

        return numbers;
    }

    // Part by part; when one is a prefix of the other, the shorter is older ("616" before "616.0").
    private static int Compare(int[] left, int[] right)
    {
        for (int i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            int order = left[i].CompareTo(right[i]);
            if (order != 0)
            {
                return order;
            }
        }

        return left.Length.CompareTo(right.Length);
    }
}
