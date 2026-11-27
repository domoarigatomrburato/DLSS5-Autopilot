using Autopilot.Core.Executables;
using Autopilot.Core.Library;
using Autopilot.Core.Routes;

namespace Autopilot.Core.Install;

/// <summary>What the first phase of an install checks: the game as read, the chosen route, the settings, the card and the plan.</summary>
internal sealed record RefusalRequest
{
    /// <param name="game">The game, as the library read it.</param>
    /// <param name="route">The chosen route's entry of the game's dropdown: its reliability and its driver warnings.</param>
    /// <param name="facts">The game and the card as the routes read them.</param>
    /// <param name="settings">The person's settings; only those the route shows for the game are read.</param>
    /// <param name="plan">The install the route wants, which the transaction would run.</param>
    public RefusalRequest(GameEntry game, RouteOption route, RouteFacts facts, RouteSettings settings, InstallPlan plan)
    {
        Game = game;
        Route = route;
        Facts = facts;
        Settings = settings;
        Plan = plan;
    }

    /// <summary>The game, as the library read it.</summary>
    public GameEntry Game { get; init; }

    /// <summary>The chosen route's entry of the game's dropdown: its reliability and its driver warnings.</summary>
    public RouteOption Route { get; init; }

    /// <summary>The game and the card as the routes read them.</summary>
    public RouteFacts Facts { get; init; }

    /// <summary>The person's settings; only those the route shows for the game are read.</summary>
    public RouteSettings Settings { get; init; }

    /// <summary>The install the route wants, which the transaction would run; its folders are read full from <see cref="InstallFolder"/> and <see cref="GameFolder"/>.</summary>
    public InstallPlan Plan
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
            InstallFolder = Path.GetFullPath(value.InstallFolder);
            GameFolder = Path.GetFullPath(value.GameFolder);
        }
    }

    /// <summary>The plan's install folder, full.</summary>
    public string InstallFolder { get; private init; } = "";

    /// <summary>The plan's game folder, full.</summary>
    public string GameFolder { get; private init; } = "";

    /// <summary>"Install anyway" on a card DLSS cannot run on: a one-off, never kept.</summary>
    public bool IgnoreGpuMismatch { get; init; }

    /// <summary>The tool's own copy of the person's frame generation files (<c>own-fg</c> in its data folder); null when it has none.</summary>
    public string? OwnFrameGenerationStore { get; init; }

    /// <summary>The Agility SDK request this Windows cannot meet, when the executable makes one.</summary>
    public AgilityUnmet? Agility { get; init; }
}
