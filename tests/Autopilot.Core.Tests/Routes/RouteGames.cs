using Autopilot.Core.Executables;
using Autopilot.Core.Gpu;
using Autopilot.Core.Graphics;
using Autopilot.Core.Routes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Autopilot.Core.Tests.Routes;

/// <summary>Games, cards and drivers as the route decision reads them.</summary>
internal static class RouteGames
{
    /// <summary>A card name for each architecture the steering cases use; null for a name the table cannot read.</summary>
    public static string CardFor(int? sm) => sm switch
    {
        75 => "NVIDIA GeForce RTX 2080",
        86 => "NVIDIA GeForce RTX 3080",
        89 => "NVIDIA GeForce RTX 4070",
        120 => "NVIDIA GeForce RTX 5080",
        _ => "NVIDIA Graphics Device",
    };

    /// <summary>The facts for an NVIDIA card of architecture <paramref name="sm"/> (null: a name that says none) on <paramref name="driver"/> (null: not read).</summary>
    public static GpuFacts Card(int? sm, string? driver) =>
        GpuFacts.From(new GpuReading([new GraphicsAdapter(Gpu.GpuReadings.Nvidia, CardFor(sm), Software: false)], driver, []));

    public static Bitness? Bits(int bits) => bits switch
    {
        32 => Bitness.Bits32,
        64 => Bitness.Bits64,
        _ => null,
    };

    public static GameRuntimes Runtimes(bool ownDlss = false, Upscaler upscaler = Upscaler.None, string? remix = null) =>
        new(ownDlss, ownDlss ? ["nvngx_dlss.dll"] : [], upscaler, upscaler == Upscaler.None ? [] : ["ffx_fsr2_api_x64.dll"], false, false, remix);

    public static RouteChoice Decide(
        GraphicsApi api,
        int bits = 64,
        bool ownDlss = false,
        Upscaler upscaler = Upscaler.None,
        string? remix = null,
        GpuFacts? gpu = null,
        SharedResults? shared = null,
        ApiDecision? detected = null,
        ILogger? logger = null) =>
        RouteDecision.Decide(
            new RouteInputs(api, Bits(bits), Runtimes(ownDlss, upscaler, remix), gpu) { Shared = shared, Detected = detected },
            logger ?? NullLogger.Instance);

    /// <summary>The offered route ids in dropdown order, comma separated.</summary>
    public static string Ids(this RouteChoice choice) => string.Join(",", choice.Options.Select(option => option.Route.Id()));

    public static string? RecommendedId(this RouteChoice choice) => choice.Recommended?.Id();

    public static RouteOption Option(this RouteChoice choice, RouteId route) => choice.Options.Single(option => option.Route == route);

    /// <summary>The message's values joined by '|'.</summary>
    public static string Joined(this RouteMessage message) => string.Join("|", message.Values);

    public static string Keys(this IEnumerable<RouteMessage> messages) => string.Join(",", messages.Select(message => message.Key));

    /// <summary>Every message the choice carries, so a test can render them all.</summary>
    public static IEnumerable<RouteMessage> Messages(this RouteChoice choice) =>
        [choice.Reason, .. choice.Notes, .. choice.Options.SelectMany(option => option.Notes.Concat(option.Warnings))];
}
