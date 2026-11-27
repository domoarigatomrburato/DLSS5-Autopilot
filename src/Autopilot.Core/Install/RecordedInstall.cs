namespace Autopilot.Core.Install;

/// <summary>The install record found in a folder, as install's reader answers for it.</summary>
/// <param name="State">Where the recorded install stands.</param>
/// <param name="Executables">The executable file names the record was written for; empty when <see cref="InstallState.Unreadable"/>.</param>
public sealed record RecordedInstall(InstallState State, IReadOnlyList<string> Executables)
{
    /// <summary>The record as read, its unsafe entries dropped; null exactly when <see cref="InstallState.Unreadable"/>.</summary>
    public InstallRecord? Record { get; init; }

    /// <summary>The game folder the reader derived from where the record sits; its entries are relative to it. Set with <see cref="Record"/>.</summary>
    /// <remarks>The record names it, so install and uninstall also check it is at or below the library's game folder before writing (sub-tasks d and e).</remarks>
    internal string? GameFolder { get; init; }

    /// <summary>Why the record cannot be read; null when it can.</summary>
    public RecordProblem? Problem { get; init; }
}
