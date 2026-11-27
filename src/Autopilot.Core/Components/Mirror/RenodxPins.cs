using Autopilot.Core.Gpu;
using Autopilot.Core.Graphics;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Components.Mirror;

/// <summary>Why the <c>renodx-dlss5</c> build is the one it is, in order of precedence.</summary>
public enum RenodxReason
{
    /// <summary>The person brings a file of their own: no build is taken from the list.</summary>
    OwnFile,

    /// <summary>The person picked a label under "dlss5 add-on".</summary>
    PersonChoice,

    /// <summary>Driver 616.64 or newer: 4.6 and 4.7 fault inside <c>D3D12Core.dll</c> on every evaluate.</summary>
    DriverFault,

    /// <summary>An OpenGL game: newer builds' workset pool never recycles under OpenGL.</summary>
    OpenGl,

    /// <summary>A feeder release below <c>v0.8.0-beta.3</c>: newer builds overlap it and <c>CreateFeature</c> dies.</summary>
    OldFeeder,

    /// <summary>Nothing applies: the newest build.</summary>
    Newest,
}

/// <summary>What the <c>renodx-dlss5</c> choice depends on.</summary>
/// <param name="ChosenLabel">The label the person picked under "dlss5 add-on"; null on Auto.</param>
/// <param name="OwnFile">Whether the person brings an add-on file of their own.</param>
/// <param name="Driver">The NVIDIA driver as people know it ("616.92"); null when not known.</param>
/// <param name="Api">The game's graphics API.</param>
/// <param name="FeederTag">The feeder release the install uses (<c>v0.15.1</c>); null off the feeder route.</param>
public sealed record RenodxInputs(string? ChosenLabel, bool OwnFile, string? Driver, GraphicsApi Api, string? FeederTag);

/// <summary>The <c>renodx-dlss5</c> build to take: a label, or none for the newest or an own file.</summary>
/// <param name="Reason">The rule that decided it.</param>
/// <param name="Label">The label wanted; null for <see cref="RenodxReason.Newest"/> and <see cref="RenodxReason.OwnFile"/>.</param>
/// <param name="Evidence">Why a pin exists, for the log and the notes; null when no pin decided.</param>
public sealed record RenodxDecision(RenodxReason Reason, string? Label, string? Evidence = null)
{
    /// <summary>Whether a pin decided, which a missing build turns into a refusal.</summary>
    public bool IsPin => Reason is RenodxReason.DriverFault or RenodxReason.OpenGl or RenodxReason.OldFeeder;
}

/// <summary>One pin: the label, when it applies (the rule and its threshold), and the evidence behind it.</summary>
internal sealed record RenodxPin(string Label, RenodxReason AppliesWhen, string? Threshold, string Evidence);

/// <summary>
/// The <c>renodx-dlss5</c> pins, shipped as data with the release and reviewed like code. Precedence:
/// the person's choice (a label or an own file), then the rows of <see cref="Table"/> in order, then
/// the newest. A pinned build that is not on the list stops the install, never the newest.
/// </summary>
public static class RenodxPins
{
    /// <summary>The label pinned on drivers from <see cref="NvidiaDriver.FirstWithRenodxFault"/> on, which the route's driver warning names.</summary>
    internal const string DriverFaultLabel = "4.55";

    /// <summary>The pins, in precedence order. The 4.55 label satisfies all three.</summary>
    internal static IReadOnlyList<RenodxPin> Table { get; } =
    [
        new(DriverFaultLabel, RenodxReason.DriverFault, NvidiaDriver.FirstWithRenodxFault,
            "4.6/4.7 fault inside D3D12Core.dll on every evaluate on driver 616.64 and newer (DLSS5-Feeder #54: 4.55 passes 300/300, 4.7 passes 0/300)"),
        new("4.60", RenodxReason.OpenGl, null,
            "4.70's workset pool never recycles under OpenGL (\"NR workset pool exhausted\" after four frames), verified on six OpenGL games"),
        new("4.55", RenodxReason.OldFeeder, "v0.8.0-beta.3",
            "newer builds overlap feeder releases below v0.8.0-beta.3 and CreateFeature dies with 0xC0000005"),
    ];

    /// <summary>The mirror's tag of every pinned build, each once (<c>renodx-dlss5-4.55</c>).</summary>
    internal static IReadOnlyList<string> Tags { get; } =
        [.. Table.Select(pin => pin.Label).Distinct(StringComparer.Ordinal).Select(label => MirrorFamilies.TagOf(BuildFamily.Renodx, label))];

    /// <summary>The build to take for these inputs, by the precedence above.</summary>
    public static RenodxDecision Decide(RenodxInputs inputs)
    {
        if (inputs.OwnFile)
        {
            return new(RenodxReason.OwnFile, null);
        }

        if (!string.IsNullOrWhiteSpace(inputs.ChosenLabel))
        {
            return new(RenodxReason.PersonChoice, inputs.ChosenLabel);
        }

        RenodxPin? pin = Table.FirstOrDefault(row => Applies(row, inputs));
        return pin is not null ? new(pin.AppliesWhen, pin.Label, pin.Evidence) : new(RenodxReason.Newest, null);
    }

    /// <summary>
    /// The entry for a decision. A person's label that is not on the list gives the newest, marked
    /// <see cref="PickedBuild.WantedMissing"/> for the caller to say; a pin that is not on the list is a refusal.
    /// </summary>
    /// <exception cref="ArgumentException">The decision is <see cref="RenodxReason.OwnFile"/>: nothing is taken from the list.</exception>
    /// <exception cref="ComponentException">
    /// The list has no <c>renodx-dlss5</c> builds (<see cref="ComponentFailure.BuildListUnavailable"/>), or a pin's
    /// build is not on it (<see cref="ComponentFailure.PinMissing"/>): the newest build is the kind of build the pin exists to avoid.
    /// </exception>
    public static PickedBuild Pick(BuildCatalog catalog, RenodxDecision decision)
    {
        if (decision.Reason == RenodxReason.OwnFile)
        {
            throw new ArgumentException("the person's own file is not taken from the build list", nameof(decision));
        }

        PickedBuild picked = catalog.Pick(BuildFamily.Renodx, decision.Label);
        return decision.IsPin && picked.WantedMissing ? throw PinMissing(decision.Label!) : picked;
    }

    private static ComponentException PinMissing(string label) =>
        new(ComponentFailure.PinMissing, Texts.Format("RenodxPinMissing", label)) { Label = label };

    private static bool Applies(RenodxPin pin, RenodxInputs inputs) => pin.AppliesWhen switch
    {
        RenodxReason.DriverFault => NvidiaDriver.AtLeast(inputs.Driver, pin.Threshold!) == true,
        RenodxReason.OpenGl => inputs.Api == GraphicsApi.OpenGL,
        RenodxReason.OldFeeder => inputs.FeederTag is { } feeder
            && new ComponentVersion(feeder).CompareTo(new ComponentVersion(pin.Threshold!)) == VersionOrder.Older,
        _ => false,
    };
}
