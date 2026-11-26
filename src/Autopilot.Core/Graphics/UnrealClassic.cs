using System.Collections.Frozen;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Graphics;

/// <summary>
/// Unreal Engine 1 and 2 (Deus Ex, UT2004, POSTAL 2, Splinter Cell: Chaos Theory, SWAT 4, Killing
/// Floor): the executable sits in <c>System</c> beside <c>Core.dll</c> and <c>Engine.dll</c>,
/// imports no graphics DLL, and loads the render device its ini names.
/// </summary>
internal static class UnrealClassic
{
    private const string Ddraw = "ddraw.dll";

    // Unreal 1 spells the key GameRenderDevice; it is asked first.
    private static readonly string[] _keys = ["GameRenderDevice", "RenderDevice"];

    // Tried when no ini names a module that is there.
    private static readonly string[] _modules = ["D3D9Drv", "D3DDrv"];

    // D3DDrv is Direct3D 8 in Unreal 2 and Direct3D 7 in Unreal 1, so its name alone settles nothing.
    private static readonly FrozenDictionary<string, GraphicsApi> _devices = new Dictionary<string, GraphicsApi>
    {
        ["d3d9drv"] = GraphicsApi.DX9,
        ["d3d10drv"] = GraphicsApi.DX10,
        ["d3d11drv"] = GraphicsApi.DX11,
        ["opengldrv"] = GraphicsApi.OpenGL,
        ["xopengldrv"] = GraphicsApi.OpenGL,
        ["vulkandrv"] = GraphicsApi.Vulkan,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static async Task<ApiDecision?> DecideAsync(GameFolder game, CancellationToken cancellationToken)
    {
        if (!IsLayout(game))
        {
            return null;
        }

        List<(string Device, string Where)> tried = [];
        if (await IniDeviceAsync(game, cancellationToken) is { } fromIni)
        {
            tried.Add(fromIni);
        }

        tried.AddRange(_modules.Where(module => game.Files.ContainsKey(module + ".dll")).Select(module => (module, Texts.Format("ApiClassicFromModule", module))));
        foreach ((string device, string where) in tried)
        {
            if (await DeviceAsync(game, device, cancellationToken) is { } reading)
            {
                return reading.Direct3D7
                    ? new ApiDecision(GraphicsApi.Unknown, ApiReason.None, Texts.Format("ApiUnrealClassicD3d7", where, reading.How))
                    : new ApiDecision(reading.Api, ApiReason.UnrealClassic, Texts.Format("ApiUnrealClassic", where, reading.How));
            }
        }

        return null;
    }

    private static bool IsLayout(GameFolder game) =>
        string.Equals(Path.GetFileName(game.Folder), "System", StringComparison.OrdinalIgnoreCase)
        && game.Files.ContainsKey("Core.dll")
        && game.Files.ContainsKey("Engine.dll");

    // The game's own ini, then Default.ini; the first that names a device decides.
    private static async Task<(string Device, string Where)?> IniDeviceAsync(GameFolder game, CancellationToken cancellationToken)
    {
        foreach (string name in new[] { Path.GetFileNameWithoutExtension(game.Executable) + ".ini", "Default.ini" })
        {
            if (!game.Files.TryGetValue(name, out string? path) || await IniFile.ReadAsync(path, cancellationToken) is not { } text)
            {
                continue;
            }

            List<IniEntry> engine = [.. IniFile.Entries(text).Where(entry => string.Equals(entry.Section, "Engine.Engine", StringComparison.OrdinalIgnoreCase))];
            foreach (string key in _keys)
            {
                IniEntry? first = engine.Find(entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));
                string device = first?.Value.Split('.', 2)[0].Trim() ?? "";
                if (device.Length > 0)
                {
                    return (device, Texts.Format("ApiClassicFromIni", device, key, Path.GetFileName(path)));
                }
            }
        }

        return null;
    }

    // The module's own import table first, then its name. The module is found by name in the folder, never opened as named.
    private static async Task<DeviceReading?> DeviceAsync(GameFolder game, string device, CancellationToken cancellationToken)
    {
        if (game.Files.TryGetValue(device + ".dll", out string? module) && await FileBitness.ReadTablesAsync(module, cancellationToken) is { } tables)
        {
            string name = Path.GetFileName(module);
            if (KnownFiles.Renderers.FirstOrDefault(renderer => tables.Imports.Contains(renderer.Name)) is ({ } imported, var api))
            {
                return new DeviceReading(api, false, Texts.Format("ApiClassicModuleImports", name, imported));
            }

            if (tables.Imports.Contains(Ddraw))
            {
                return new DeviceReading(GraphicsApi.Unknown, true, Texts.Format("ApiClassicModuleImports", name, Ddraw));
            }
        }

        return _devices.TryGetValue(device, out GraphicsApi byName)
            ? new DeviceReading(byName, false, Texts.Format("ApiClassicByName", device, byName))
            : null;
    }

    private sealed record DeviceReading(GraphicsApi Api, bool Direct3D7, string How);
}
