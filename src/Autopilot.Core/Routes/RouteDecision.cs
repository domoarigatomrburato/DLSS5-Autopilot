using Autopilot.Core.Executables;
using Autopilot.Core.Gpu;
using Autopilot.Core.Graphics;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Routes;

/// <summary>
/// Which routes a game is offered and which is recommended, and why: the rules table (first match
/// wins), then uplift, the Remix override, the RTX 50 steer to OptiScaler, and the shared results.
/// The driver adds warnings and never moves the recommendation.
/// </summary>
public static class RouteDecision
{
    /// <summary>The routes offered for the game in <paramref name="inputs"/> and the one recommended.</summary>
    /// <param name="inputs">The game, the card and the driver, and the shared results if any.</param>
    /// <param name="logger">Told when the shared results are malformed and left out.</param>
    public static RouteChoice Decide(RouteInputs inputs, ILogger logger)
    {
        // The install refuses both, so the list offers nothing rather than a route that cannot go in.
        if (inputs.Bitness is not { } bitness)
        {
            return new RouteChoice { Options = [], Reason = new RouteMessage("RouteChooseArchitecture"), Asks = RouteQuestion.Architecture };
        }

        if (inputs.Api == GraphicsApi.DX8 && bitness == Bitness.Bits64)
        {
            return new RouteChoice { Options = [], Reason = new RouteMessage("RouteDx8On64") };
        }

        RouteDraft draft = RouteRows.For(inputs.Api, bitness, inputs.Runtimes);
        if (inputs.Api != GraphicsApi.DX8)
        {
            draft = draft with { Options = [.. draft.Options, RouteId.Uplift] };
        }

        int? sm = inputs.Gpu?.Card?.Sm;
        return Choice(Steer(draft, inputs, sm, logger), inputs, bitness, sm);
    }

    // Steps 2 to 5: a Remix game is recommended remix and never moved; any other is steered by the card, then the shared results.
    private static RouteDraft Steer(RouteDraft draft, RouteInputs inputs, int? sm, ILogger logger)
    {
        if (inputs.Runtimes.RemixRuntime is { } trex)
        {
            return Remix(draft, trex);
        }

        RouteDraft steered = SteerToOptiScaler(draft, inputs, sm);
        return SharedSteer.Apply(steered, inputs.Shared, route => RouteFit.Of(route, inputs.Api, inputs.Runtimes, sm).Usable, logger);
    }

    // Step 2: remix first and recommended; the rest stay listed, and none can be recommended.
    private static RouteDraft Remix(RouteDraft draft, string trex) =>
        new([RouteId.Remix, .. draft.Options], RouteId.Remix, new RouteMessage("RouteRemix", trex))
        {
            Notes = [new RouteMessage("RouteNoteRemixOthersRefused")],
        };

    // Steps 3 and 4: on an RTX 50 card a D3D12 game with DLSS of its own, or with FSR or XeSS and
    // no DLSS, gets OptiScaler for its model-resolution dial. Other cards, and a card not known, keep the rule's pick.
    private static RouteDraft SteerToOptiScaler(RouteDraft draft, RouteInputs inputs, int? sm)
    {
        if (!(sm >= CardNames.Rtx50) || inputs.Api != GraphicsApi.DX12 || !draft.Options.Contains(RouteId.OptiScaler))
        {
            return draft;
        }

        GameRuntimes runtimes = inputs.Runtimes;
        if (runtimes.OwnDlss && draft.Recommended == RouteId.Native)
        {
            return draft with { Recommended = RouteId.OptiScaler, Reason = new RouteMessage("RouteOptiScalerRtx50") };
        }

        return runtimes.Upscaler != Upscaler.None && draft.Recommended == RouteId.Feeder
            ? draft with { Recommended = RouteId.OptiScaler, Reason = new RouteMessage("RouteOptiScalerUpscalerRtx50", RouteRows.UpscalerName(runtimes.Upscaler)) }
            : draft;
    }

    private static RouteChoice Choice(RouteDraft draft, RouteInputs inputs, Bitness bitness, int? sm)
    {
        bool standalone = draft.Options.Contains(RouteId.Standalone);
        return new RouteChoice
        {
            Options = [.. draft.Options.Select(route => Option(route, inputs, bitness, sm, standalone))],
            Recommended = draft.Recommended,
            Reason = draft.Reason,
            Notes = draft.Notes,
            Asks = draft.Asks,
            ApiEvidence = draft.Asks == RouteQuestion.Api ? inputs.Detected : null,
            MovedBy = draft.MovedBy,
        };
    }

    private static RouteOption Option(RouteId route, RouteInputs inputs, Bitness bitness, int? sm, bool standaloneOffered)
    {
        Fit fit = RouteFit.Of(route, inputs.Api, inputs.Runtimes, sm);
        return new RouteOption(route, fit.Usable, fit.Notes, RouteFit.Level(route, inputs.Api, bitness), RouteWarnings.For(route, inputs.Gpu, standaloneOffered));
    }
}
