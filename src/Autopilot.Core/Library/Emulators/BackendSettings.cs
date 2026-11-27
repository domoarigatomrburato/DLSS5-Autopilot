using System.Collections.Frozen;
using Autopilot.Core.Archives;

namespace Autopilot.Core.Library.Emulators;

/// <summary>library.md's config table: where each emulator's backend setting lives, its names, and which values are already DXGI.</summary>
internal static class BackendSettings
{
    private const string Portable = "portable.txt";

    private static readonly FrozenDictionary<string, BackendSetting> _byProfile = new Dictionary<string, BackendSetting>
    {
        ["DuckStation"] = new(
            Sync(place => [place.Marked(Portable, "settings.ini") ? place.Beside("settings.ini") : null, place.InDocuments("DuckStation", "settings.ini"), place.InLocalAppData("DuckStation", "settings.ini")]),
            bytes => ConfigText.KeyValue(bytes, "GPU", "Renderer"),
            Names(("D3D11", "D3D11"), ("D3D12", "D3D12"), ("Vulkan", "Vulkan"), ("OpenGL", "OpenGL"), ("Automatic", "Automatic"), ("Software", "Software")),
            Set("D3D11", "D3D12")),
        ["Pcsx2"] = new(
            Sync(place => [place.Marked("portable.ini", Portable) ? place.Beside("inis", "PCSX2.ini") : null, place.InDocuments("PCSX2", "inis", "PCSX2.ini")]),
            bytes => ConfigText.KeyValue(bytes, "EmuCore/GS", "Renderer"),
            Names(("-1", "Automatic"), ("3", "D3D11"), ("15", "D3D12"), ("12", "OpenGL"), ("14", "Vulkan"), ("13", "Software"), ("11", "Null")),
            Set("3", "15")),
        ["Dolphin"] = new(
            Sync(place =>
                [
                    place.Marked(Portable) ? place.Beside("User", "Config", "Dolphin.ini") : null,
                    place.InDocuments("Dolphin Emulator", "Config", "Dolphin.ini"),
                    place.InAppData("Dolphin Emulator", "Config", "Dolphin.ini"),
                ]),
            bytes => ConfigText.KeyValue(bytes, "Core", "GFXBackend"),
            Names(("D3D", "D3D11"), ("D3D12", "D3D12"), ("Vulkan", "Vulkan"), ("OGL", "OpenGL"), ("Software Renderer", "Software"), ("Null", "Null")),
            Set("D3D", "D3D12")),
        ["Ppsspp"] = new(
            PpssppAsync,
            bytes => ConfigText.KeyValue(bytes, "Graphics", "GraphicsBackend"),
            Names(("0", "OpenGL"), ("2", "D3D11"), ("3", "Vulkan")),
            Set("2", "2 (DIRECT3D11)", "DIRECT3D11"),
            LeadingNumber: true),
        ["Xenia"] = new(
            Sync(Xenia),
            bytes => ConfigText.KeyValue(bytes, "GPU", "gpu"),
            Names(("d3d12", "D3D12"), ("vulkan", "Vulkan"), ("any", "Automatic"), ("null", "Null")),
            Set("d3d12")),
        ["RetroArch"] = new(
            Sync(place => [place.Beside("retroarch.cfg"), place.InAppData("RetroArch", "retroarch.cfg")]),
            bytes => ConfigText.KeyValue(bytes, null, "video_driver"),
            Names(("d3d11", "D3D11"), ("d3d12", "D3D12"), ("gl", "OpenGL"), ("glcore", "OpenGL"), ("vulkan", "Vulkan"), ("d3d9", "D3D9"), ("sdl2", "SDL"), ("gdi", "GDI")),
            Set("d3d11", "d3d12")),
        ["Rpcs3"] = new(
            Sync(place => [place.Beside("config.yml")]),
            bytes => ConfigText.YamlValue(bytes, "Video", "Renderer"),
            Names(("vulkan", "Vulkan"), ("opengl", "OpenGL"), ("null", "Null")),
            Set()),
        ["Cemu"] = new(
            Sync(place => [place.Beside("settings.xml"), place.InAppData("Cemu", "settings.xml")]),
            bytes => ConfigText.XmlValue(bytes, "Graphic", "api"),
            Names(("0", "OpenGL"), ("1", "Vulkan")),
            Set()),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The setting of <paramref name="profile"/>; null for an emulator whose config this tool does not read.</summary>
    public static BackendSetting? For(EmulatorProfile profile) => _byProfile.GetValueOrDefault(profile.Key);

    // Canary names its file xenia-canary.config.toml; each build looks for its own first.
    private static IEnumerable<string?> Xenia(ConfigPlace place)
    {
        string[] names = place.ExecutableName.Contains("canary", StringComparison.OrdinalIgnoreCase)
            ? ["xenia-canary.config.toml", "xenia.config.toml"]
            : ["xenia.config.toml", "xenia-canary.config.toml"];
        string?[] folders = [place.Marked(Portable) ? place.Folder : null, place.InDocuments("Xenia")];
        return folders.OfType<string>().SelectMany(folder => names.Select(name => Path.Join(folder, name)));
    }

    // installed.txt beside the executable names the memory stick folder (Documents\PPSSPP when it is
    // empty) and puts it before the one beside the executable; without it, the one beside comes first.
    private static async Task<IEnumerable<string?>> PpssppAsync(ConfigPlace place, CancellationToken cancellationToken)
    {
        string installed = place.Beside("installed.txt");
        string?[] sticks = await ConfigText.ReadAsync(installed, cancellationToken) is { } bytes
            ? [InstalledStick(place, ConfigText.Lines(bytes)[0].Trim()), place.InDocuments("PPSSPP"), place.Beside("memstick")]
            : [place.Beside("memstick"), place.InDocuments("PPSSPP")];
        return sticks.OfType<string>().Select(stick => Path.Join(stick, "PSP", "SYSTEM", "ppsspp.ini"));
    }

    // A path the file names is untrusted: relative, it stays below the executable's folder; absolute,
    // it must already be there.
    private static string? InstalledStick(ConfigPlace place, string named)
    {
        if (named.Length == 0)
        {
            return null;
        }

        try
        {
            return SafePath.Join(place.Folder, named);
        }
        catch (UnsafePathException)
        {
            return Path.IsPathFullyQualified(named) && IsBelow(place.Folder, named) ? named : null;
        }
    }

    private static bool IsBelow(string folder, string path)
    {
        string relative = Path.GetRelativePath(folder, path);
        return relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }

    private static Func<ConfigPlace, CancellationToken, Task<IEnumerable<string?>>> Sync(Func<ConfigPlace, IEnumerable<string?>> locate) =>
        (place, _) => Task.FromResult(locate(place));

    private static FrozenDictionary<string, string> Names(params (string Raw, string Name)[] names) =>
        names.ToFrozenDictionary(pair => pair.Raw, pair => pair.Name, StringComparer.OrdinalIgnoreCase);

    private static FrozenSet<string> Set(params string[] values) => values.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
