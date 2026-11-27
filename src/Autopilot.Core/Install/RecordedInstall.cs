namespace Autopilot.Core.Install;

/// <summary>The install record found in a folder.</summary>
/// <param name="State">Where the recorded install stands.</param>
/// <param name="Executables">The executable file names the record was written for; empty when <see cref="InstallState.Unreadable"/>.</param>
public sealed record RecordedInstall(InstallState State, IReadOnlyList<string> Executables)
{
    /// <summary>
    /// The files the recorded install wrote, as paths relative to the install folder with either kind
    /// of slash, compared ignoring case; empty until install (M4) fills it, and when <see cref="InstallState.Unreadable"/>.
    /// A file listed here is this tool's, never evidence of what the game ships.
    /// </summary>
    public IReadOnlyList<string> Files { get; init; } = [];
}
