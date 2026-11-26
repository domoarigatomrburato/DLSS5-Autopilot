using Autopilot.Core.Resources;

namespace Autopilot.Core.Executables;

/// <summary>
/// An Agility SDK request this Windows cannot meet: the game ships no <c>D3D12Core.dll</c> at the
/// path it exports, and the system's own is older than the version it asks for, so no Direct3D 12
/// device can be made in its process.
/// </summary>
/// <param name="Executable">The executable's path.</param>
/// <param name="Requested">The SDK version the executable asks for.</param>
/// <param name="Path">The path the executable exports, as exported.</param>
/// <param name="SystemVersion">The SDK version of Windows' own <c>D3D12Core.dll</c>.</param>
public sealed record AgilityUnmet(string Executable, uint Requested, string Path, uint SystemVersion)
{
    /// <summary>The verdict in words: the executable, the version it asks for, its path, and what Windows has.</summary>
    public string Explain() =>
        Texts.Format("AgilityUnmet", System.IO.Path.GetFileName(Executable), Requested, Path, SystemVersion);
}
