using System.Collections.Frozen;

namespace Autopilot.Core.Library.Emulators;

/// <summary>Where one emulator keeps its render backend, and how its values read.</summary>
/// <param name="Locate">The candidate config files, best first; a null one is a folder the machine does not set.</param>
/// <param name="Value">The setting's raw value in the file's bytes, or null when it is not there.</param>
/// <param name="Names">Raw value to the status's words, any case; a value not listed is shown as written.</param>
/// <param name="Dxgi">The raw values that already present through Direct3D 11 or 12, any case.</param>
/// <param name="LeadingNumber">The emulator reads only the number before a space: PPSSPP writes <c>3 (VULKAN)</c>.</param>
internal sealed record BackendSetting(
    Func<ConfigPlace, CancellationToken, Task<IEnumerable<string?>>> Locate,
    Func<byte[], string?> Value,
    FrozenDictionary<string, string> Names,
    FrozenSet<string> Dxgi,
    bool LeadingNumber = false)
{
    /// <summary>The value in the status's words.</summary>
    public string NameOf(string raw) => Names.GetValueOrDefault(Read(raw)) ?? raw;

    /// <summary>Whether the value already presents through Direct3D 11 or 12.</summary>
    public bool IsDxgi(string raw) => Dxgi.Contains(raw) || Dxgi.Contains(Read(raw));

    private string Read(string raw) => LeadingNumber ? raw.Split(' ', 2)[0] : raw;
}
