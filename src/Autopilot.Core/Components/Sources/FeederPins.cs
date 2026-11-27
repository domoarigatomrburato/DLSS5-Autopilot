using Autopilot.Core.Resources;

namespace Autopilot.Core.Components.Sources;

/// <summary>What an install asks of the feeder build, each with the first build that can do it.</summary>
public enum FeederNeed
{
    /// <summary>A Direct3D 10 game.</summary>
    Direct3D10,

    /// <summary>The 64-bit helper-process mode.</summary>
    HelperProcess,

    /// <summary>The built-in motion-vector provider.</summary>
    BuiltInProvider,

    /// <summary>An HDR display.</summary>
    HdrDisplay,
}

/// <summary>One feeder pin: the first build that meets a need, and whether an older one is refused or only warned about.</summary>
public sealed record FeederPin(FeederNeed Need, ComponentVersion Minimum, bool Refuses);

/// <summary>What the feeder pins say about one build: refusals stop the install before anything is written; warnings are shown.</summary>
public sealed record FeederPinCheck(IReadOnlyList<string> Refusals, IReadOnlyList<string> Warnings)
{
    public bool Allowed => Refusals.Count == 0;
}

/// <summary>
/// The feeder pins, as data shipped with the release (the feeder's own release notes are the
/// evidence). The install runs <see cref="Check"/> before its first write.
/// </summary>
public static class FeederPins
{
    public static IReadOnlyList<FeederPin> All { get; } =
    [
        new(FeederNeed.Direct3D10, new("v0.13.1-beta.1"), Refuses: true),
        new(FeederNeed.HelperProcess, new("v1.18.0-beta.1"), Refuses: true),
        new(FeederNeed.BuiltInProvider, new("v2.0.0"), Refuses: true),
        new(FeederNeed.HdrDisplay, new("v0.15.1"), Refuses: false),
    ];

    /// <summary>
    /// The pins <paramref name="feeder"/> falls below for <paramref name="needs"/>. A version whose
    /// numbers cannot be compared is never called older.
    /// </summary>
    public static FeederPinCheck Check(ComponentVersion feeder, IEnumerable<FeederNeed> needs)
    {
        ArgumentNullException.ThrowIfNull(feeder);
        FeederPin[] missed = [.. All.Where(pin => needs.Contains(pin.Need) && feeder.CompareTo(pin.Minimum) == VersionOrder.Older)];
        return new(
            [.. missed.Where(pin => pin.Refuses).Select(pin => Message(pin, feeder))],
            [.. missed.Where(pin => !pin.Refuses).Select(pin => Message(pin, feeder))]);
    }

    private static string Message(FeederPin pin, ComponentVersion feeder) =>
        Texts.Format("FeederPin" + pin.Need, feeder.Label, pin.Minimum.Label);
}
