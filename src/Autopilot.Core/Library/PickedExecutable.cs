using Autopilot.Core.Install;

namespace Autopilot.Core.Library;

/// <summary>The executable chosen for a game, its candidates, and the install recorded for it.</summary>
/// <param name="Executable">The executable, or null when there is none.</param>
/// <param name="Candidates">Every executable found, best first.</param>
/// <param name="InstallFolder">Where the record was found, only when it is not the executable's folder.</param>
/// <param name="Install">What the record says, or null when none was found.</param>
internal sealed record PickedExecutable(string? Executable, IReadOnlyList<string> Candidates, string? InstallFolder, InstallState? Install);
