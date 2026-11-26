namespace Autopilot.Core.Library.Stores;

/// <summary>
/// One store or folder search. It reads files and the registry only, never the network, and never
/// writes. A source that finds nothing returns an empty list; one bad manifest or value is skipped;
/// a failure of the whole source surfaces as an exception for the scan to report.
/// </summary>
public interface IGameSource
{
    GameSource Source { get; }

    /// <summary>The installed games, each folder once.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    Task<IReadOnlyList<StoreGame>> FindAsync(CancellationToken cancellationToken);
}
