using Autopilot.Core.Executables;
using Autopilot.Core.Gpu;
using Autopilot.Core.Graphics;

namespace Autopilot.Core.Routes;

/// <summary>What the route decision reads: the game as detected or as the person set it, the card and the driver.</summary>
/// <param name="Api">The graphics API in use: the person's choice, else what the executable said.</param>
/// <param name="Bitness">The architecture, or null when it is not known.</param>
/// <param name="Runtimes">What the game ships of its own.</param>
/// <param name="Gpu">The card and the driver; null when they could not be read, which never warns and never counts as RTX 50.</param>
public sealed record RouteInputs(GraphicsApi Api, Bitness? Bitness, GameRuntimes Runtimes, GpuFacts? Gpu)
{
    /// <summary>What the executable said about the API, shown when the person is asked for it; null when it was not read.</summary>
    public ApiDecision? Detected { get; init; }

    /// <summary>The community's shared results, or null when there is no list (offline, or none fetched).</summary>
    public SharedResults? Shared { get; init; }
}
