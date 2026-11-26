namespace Autopilot.Core.Platform;

/// <summary>Reads the hardware values the EA app keys its install file to.</summary>
public interface IHardwareIdentity
{
    /// <summary>The machine's values; null when any of them cannot be read, and the EA app's file is then skipped.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    Task<HardwareIdentity?> ReadAsync(CancellationToken cancellationToken);
}
