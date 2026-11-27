using System.Collections.Frozen;
using Autopilot.Core.Graphics;
using Autopilot.Core.Library.Stores;

namespace Autopilot.Core.Library.Emulators;

/// <summary>The known emulators, and which of them an executable is.</summary>
internal static class EmulatorProfiles
{
    private const GraphicsApi Direct3D = GraphicsApi.Dxgi;
    private const GraphicsApi Vulkan = GraphicsApi.Vulkan;
    private const GraphicsApi OpenGL = GraphicsApi.OpenGL;

    /// <summary>library.md's table: key, name, system, Direct3D or what it draws with instead, whether it has a note, executables.</summary>
    public static readonly IReadOnlyList<EmulatorProfile> All =
    [
        new("DuckStation", "DuckStation", "PlayStation 1", Direct3D, true, "duckstation-qt-x64.exe", "duckstation-nogui-x64.exe", "duckstation.exe"),
        new("Pcsx2", "PCSX2", "PlayStation 2", Direct3D, true, "pcsx2-qt.exe", "pcsx2x64.exe", "pcsx2x64-avx2.exe", "pcsx2.exe"),
        new("Dolphin", "Dolphin", "GameCube / Wii", Direct3D, false, "Dolphin.exe", "DolphinQt.exe"),
        new("Ppsspp", "PPSSPP", "PSP", Direct3D, false, "PPSSPPWindows64.exe", "PPSSPPWindows.exe"),
        new("Xenia", "Xenia", "Xbox 360", Direct3D, false, "xenia.exe", "xenia_canary.exe"),
        new("Cemu", "Cemu", "Wii U", Vulkan, true, "Cemu.exe"),
        new("Rpcs3", "RPCS3", "PlayStation 3", Vulkan, false, "rpcs3.exe"),
        new("Ryujinx", "Ryujinx", "Switch", Vulkan, false, "Ryujinx.exe", "Ryujinx.Ava.exe", "Ryujinx.Headless.SDL2.exe"),
        new("Yuzu", "yuzu / suyu / Eden / Citron", "Switch", Vulkan, false, "yuzu.exe", "suyu.exe", "eden.exe", "citron.exe", "sudachi.exe"),
        new("ShadPs4", "shadPS4", "PlayStation 4", Vulkan, false, "shadPS4.exe", "shadps4.exe"),
        new("Azahar", "Azahar / Citra / Lime3DS", "3DS", Vulkan, false, "azahar.exe", "citra.exe", "citra-qt.exe", "lime3ds.exe"),
        new("MelonDs", "melonDS", "DS", OpenGL, false, "melonDS.exe"),
        new("Flycast", "Flycast", "Dreamcast", Direct3D, false, "flycast.exe"),
        new("Xemu", "xemu", "Xbox", Vulkan, false, "xemu.exe"),
        new("Vita3K", "Vita3K", "PS Vita", Vulkan, false, "Vita3K.exe"),
        new("RetroArch", "RetroArch", "multi-system", Direct3D, false, "retroarch.exe"),
        new("Mgba", "mGBA", "Game Boy Advance", OpenGL, false, "mGBA.exe"),
        new("Snes9x", "Snes9x", "SNES", Direct3D, false, "snes9x-x64.exe", "snes9x.exe"),
        new("Play", "Play!", "PlayStation 2", Vulkan, false, "Play.exe"),
    ];

    // Names other programs use too: a game called Play.exe from a store is not Play!.
    private static readonly FrozenSet<string> _generic = new[] { "Play.exe", "Dolphin.exe", "Cemu.exe", "xenia.exe" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, EmulatorProfile> _byExecutable =
        All.SelectMany(profile => profile.Executables.Select(executable => KeyValuePair.Create(executable, profile))).ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The profile of the executable at <paramref name="path"/>, by its file name in any case. A name
    /// other programs use too counts only for an entry the emulator search found or the person added.
    /// </summary>
    public static EmulatorProfile? For(string path, GameSource source)
    {
        string name = Path.GetFileName(path.Replace('\\', '/'));
        if (!_byExecutable.TryGetValue(name, out EmulatorProfile? profile))
        {
            return null;
        }

        return !_generic.Contains(name) || source is GameSource.Emulator or GameSource.Manual ? profile : null;
    }
}
