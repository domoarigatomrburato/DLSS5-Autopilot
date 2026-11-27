namespace Autopilot.Core.Platform;

/// <summary>Whether the tool runs as administrator, so it can refuse what would need de-elevation (starting a game, an all-users registration).</summary>
public interface IElevation
{
    /// <summary>Whether this process's token is elevated.</summary>
    bool IsElevated { get; }
}
