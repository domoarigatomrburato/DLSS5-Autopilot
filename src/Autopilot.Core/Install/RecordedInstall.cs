namespace Autopilot.Core.Install;

/// <summary>The install record found in a folder.</summary>
/// <param name="State">Where the recorded install stands.</param>
/// <param name="Executables">The executable file names the record was written for; empty when <see cref="InstallState.Unreadable"/>.</param>
public sealed record RecordedInstall(InstallState State, IReadOnlyList<string> Executables);
