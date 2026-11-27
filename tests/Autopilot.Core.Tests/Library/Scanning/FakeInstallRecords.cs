using Autopilot.Core.Install;

namespace Autopilot.Core.Tests.Library.Scanning;

/// <summary>Install's records, by folder, ignoring case and a trailing separator.</summary>
internal sealed class FakeInstallRecords : IInstallRecords
{
    private readonly Dictionary<string, RecordedInstall> _records = [with(StringComparer.OrdinalIgnoreCase)];

    public void Add(string folder, InstallState state, params string[] executables) => _records[Normal(folder)] = new RecordedInstall(state, executables);

    public Task<RecordedInstall?> FindAsync(string folder, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_records.GetValueOrDefault(Normal(folder)));
    }

    private static string Normal(string folder) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
}
