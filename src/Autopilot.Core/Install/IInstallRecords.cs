namespace Autopilot.Core.Install;

/// <summary>Install's reader of its own records: the one way other areas learn what was installed where.</summary>
public interface IInstallRecords
{
    /// <summary>The record in <paramref name="folder"/>, or null when that folder holds none.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    Task<RecordedInstall?> FindAsync(string folder, CancellationToken cancellationToken);
}
