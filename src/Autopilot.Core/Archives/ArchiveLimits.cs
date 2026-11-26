using System.Globalization;

namespace Autopilot.Core.Archives;

/// <summary>
/// How far an archive may expand. The defaults are the ceiling: a caller may lower a limit, never
/// raise it.
/// </summary>
public sealed record ArchiveLimits
{
    private const int DefaultMaxEntries = 50_000;
    private const long DefaultMaxTotalBytes = 16L << 30;
    private const int DefaultMaxRatio = 200;
    private const long DefaultRatioCheckedAbove = 1L << 20;

    public static ArchiveLimits Default { get; } = new();

    /// <summary>Entries of any kind, folders included.</summary>
    public int MaxEntries { get; init => field = Lowered(value, DefaultMaxEntries); } = DefaultMaxEntries;

    /// <summary>The expanded size of every member together.</summary>
    public long MaxTotalBytes { get; init => field = Lowered(value, DefaultMaxTotalBytes); } = DefaultMaxTotalBytes;

    /// <summary>
    /// Expanded size over compressed size: per member for zip, over the whole archive for tar.gz and
    /// 7z, whose compressed sizes are known only for the whole stream.
    /// </summary>
    public int MaxRatio { get; init => field = Lowered(value, DefaultMaxRatio); } = DefaultMaxRatio;

    /// <summary>The ratio is checked only once the expanded size is over this.</summary>
    public long RatioCheckedAbove { get; init => field = Lowered(value, DefaultRatioCheckedAbove); } = DefaultRatioCheckedAbove;

    /// <summary>Every limit as text, so equal limits read the same.</summary>
    internal string CanonicalKey() =>
        string.Create(CultureInfo.InvariantCulture, $"{MaxEntries} {MaxTotalBytes} {MaxRatio} {RatioCheckedAbove}");

    private static T Lowered<T>(T value, T ceiling)
        where T : System.Numerics.INumber<T>
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, ceiling);
        return value;
    }
}
