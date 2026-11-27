namespace Autopilot.Core.Routes;

/// <summary>How likely a route is to work, per route and API; anything but stable is a warning before the install.</summary>
public enum RouteReliability
{
    /// <summary>Native, and the feeder on 64-bit D3D11 and D3D12.</summary>
    Stable,

    /// <summary>Works in many games, not all, or has met few of them.</summary>
    Beta,

    /// <summary>Few games tested, or a path known to fail often.</summary>
    Experimental,
}
