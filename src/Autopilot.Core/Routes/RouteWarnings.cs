using Autopilot.Core.Components.Mirror;
using Autopilot.Core.Gpu;

namespace Autopilot.Core.Routes;

/// <summary>
/// What the driver means for each route, and OptiScaler's card-and-driver note. A driver that is
/// not known, or not dotted integers, never warns; the warnings never move the recommendation.
/// </summary>
internal static class RouteWarnings
{
    /// <summary>The warnings shown with <paramref name="route"/> for this card and driver.</summary>
    public static RouteMessage[] For(RouteId route, GpuFacts? gpu, bool standaloneOffered)
    {
        RouteMessage[] driver = Driver(route, gpu?.Driver, standaloneOffered);
        return route == RouteId.OptiScaler && OptiScalerNote(gpu) is { } note ? [.. driver, note] : driver;
    }

    private static RouteMessage[] Driver(RouteId route, string? driver, bool standaloneOffered)
    {
        // Every route but remix ends in NVIDIA's own runtime; remix carries the neural pass itself.
        if (route == RouteId.Remix || driver is null)
        {
            return [];
        }

        if (NvidiaDriver.AtLeast(driver, NvidiaDriver.FirstWithNeuralRuntime) == false)
        {
            return [new RouteMessage("RouteDriverTooOld", driver, NvidiaDriver.FirstWithNeuralRuntime)];
        }

        if (NvidiaDriver.AtLeast(driver, NvidiaDriver.FirstWithRenodxFault) != true)
        {
            return [];
        }

        RouteMessage fault = Fault(route, driver);
        return RouteComponents.LoadsRenodx(route) && standaloneOffered ? [fault, new RouteMessage("RouteDriverTryStandalone")] : [fault];
    }

    private static RouteMessage Fault(RouteId route, string driver) => route switch
    {
        RouteId.Native or RouteId.Bridge or RouteId.Feeder =>
            new RouteMessage("RouteDriverRenodxFault", driver, NvidiaDriver.FirstWithRenodxFault, RenodxPins.DriverFaultLabel, NvidiaDriver.FirstWithNeuralRuntime),
        RouteId.Renodx => new RouteMessage("RouteDriverShortFuse", driver, NvidiaDriver.FirstWithRenodxFault, RenodxPins.DriverFaultLabel, NvidiaDriver.FirstWithNeuralRuntime),
        RouteId.Standalone => new RouteMessage("RouteDriverStandalone", driver, NvidiaDriver.FirstWithRenodxFault, NvidiaDriver.FirstWithNeuralRuntime),
        _ => new RouteMessage("RouteDriverGeneric", driver, NvidiaDriver.FirstWithRenodxFault, NvidiaDriver.FirstWithNeuralRuntime),
    };

    // OptiScaler's author states an RTX 50 card is required: said for a card not known, a card below
    // RTX 50, and an RTX 50 card on a driver older than the neural-rendering runtime.
    private static RouteMessage? OptiScalerNote(GpuFacts? gpu)
    {
        if (gpu?.Card?.Sm is not { } sm)
        {
            return new RouteMessage("RouteOptiScalerCardUnknown");
        }

        if (sm < CardNames.Rtx50)
        {
            return new RouteMessage("RouteOptiScalerOlderCard");
        }

        return NvidiaDriver.AtLeast(gpu.Driver, NvidiaDriver.FirstWithNeuralRuntime) == false
            ? new RouteMessage("RouteOptiScalerDriver", NvidiaDriver.FirstWithNeuralRuntime, gpu.Driver!)
            : null;
    }
}
