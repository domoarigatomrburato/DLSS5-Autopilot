using System.Text;
using Autopilot.Core.Tests.Executables;

namespace Autopilot.Core.Tests.TestBeds;

/// <summary>
/// install.md's "Fixtures", game folders, the entries that are plain files: each a list of
/// <see cref="TreeFile"/> to pass to <see cref="GameTree.Of"/>, alone or combined. DLLs are x64 PE stubs.
/// </summary>
internal static class GameFolders
{
    /// <summary>The key the Remix route sets in <c>rtx.conf</c> for the neural runtime (remix.md).</summary>
    public const string RtxNeuralKey = "rtx.neuralRendering.enable";

    /// <summary>A folder with no pre-existing files.</summary>
    public static TreeFile[] Empty => [];

    /// <summary>The game's own <c>dxgi.dll</c>, <c>nvngx_dlss.dll</c>, <c>d3dcompiler_47.dll</c>, and the person's own <c>ReShade.ini</c>.</summary>
    public static TreeFile[] OwnFiles =>
    [
        TreeFile.Bytes("dxgi.dll", Dll()),
        TreeFile.Bytes("nvngx_dlss.dll", Dll()),
        TreeFile.Bytes("d3dcompiler_47.dll", Dll()),
        TreeFile.Text("ReShade.ini", "[GENERAL]\r\nEffectSearchPaths=.\\my-shaders\r\n"),
    ];

    /// <summary>An OptiScaler the person installed by hand under <c>dxgi.dll</c>, with its <c>OptiScaler.ini</c>.</summary>
    public static TreeFile[] HandInstalledOptiScaler =>
    [
        TreeFile.Bytes("dxgi.dll", Markers.OptiScalerDll),
        TreeFile.Text("OptiScaler.ini", "[Upscalers]\r\nDx12Upscaler=auto\r\n"),
    ];

    /// <summary>A second ReShade, installed under <c>d3d11.dll</c>.</summary>
    public static TreeFile[] SecondReShade => [TreeFile.Bytes("d3d11.dll", Markers.ReShadeDll)];

    /// <summary>An RTX Remix mod: its runtime <c>.trex\d3d9.dll</c> and its <c>rtx.conf</c>, written as <paramref name="conf"/> says.</summary>
    public static TreeFile[] Remix(RtxConf conf) =>
    [
        TreeFile.Bytes(@".trex\d3d9.dll", Dll()),
        TreeFile.Text("rtx.conf", conf switch
        {
            RtxConf.NoFinalNewline => "rtx.enableRaytracing = True\nrtx.fallbackLightMode = 2",
            RtxConf.KeyFalse => $"rtx.enableRaytracing = True\n{RtxNeuralKey} = False\nrtx.fallbackLightMode = 2\n",
            _ => "rtx.enableRaytracing = True\nrtx.fallbackLightMode = 2\n",
        }),
    ];

    /// <summary>Unreal's layout: the launcher stub at the root, the game under <c>Binaries\Win64</c>, and a plugin's own <c>nvngx_dlss.dll</c> deep below.</summary>
    public static TreeFile[] Unreal =>
    [
        TreeFile.Bytes("Game.exe", Dll()),
        TreeFile.Bytes(@"Binaries\Win64\game.exe", Dll()),
        TreeFile.Bytes(@"Plugins\DLSS\Binaries\ThirdParty\Win64\nvngx_dlss.dll", Dll()),
    ];

    /// <summary>An x64 PE stub, followed by <paramref name="marker"/> when given.</summary>
    public static byte[] Dll(string? marker = null) => [.. new TestPe().ToBytes(), .. Encoding.ASCII.GetBytes(marker ?? "")];
}

/// <summary>How the Remix fixture's <c>rtx.conf</c> is written.</summary>
internal enum RtxConf
{
    /// <summary>Ends with a newline, without the neural key.</summary>
    FinalNewline,

    /// <summary>Ends without a newline, without the neural key.</summary>
    NoFinalNewline,

    /// <summary>Holds the neural key already, set to <c>False</c>.</summary>
    KeyFalse,
}

/// <summary>
/// What makes a test file look like a component's own, as <c>ContentMarks</c> reads it: a DLL of 1 MB or more naming
/// ReShade in ASCII, or OptiScaler's original file name in UTF-16.
/// </summary>
internal static class Markers
{
    private const int MarkedSize = 1 << 20;

    /// <summary>A DLL ReShade's mark tells as ReShade.</summary>
    public static byte[] ReShadeDll => Marked(Encoding.ASCII.GetBytes("ReShade"));

    /// <summary>A DLL OptiScaler's mark tells as OptiScaler, whatever it is called.</summary>
    public static byte[] OptiScalerDll => Marked(Encoding.Unicode.GetBytes("OptiScaler.dll"));

    /// <summary>An OptiScaler DLL that also names ReShade, as one built with ReShade support does.</summary>
    public static byte[] OptiScalerNamingReShade => Marked([.. Encoding.Unicode.GetBytes("OptiScaler.dll"), .. Encoding.ASCII.GetBytes("ReShade")]);

    /// <summary>A Special K DLL, which can load ReShade and names it.</summary>
    public static byte[] SpecialKNamingReShade => Marked(Encoding.ASCII.GetBytes("SpecialK ReShade"));

    /// <summary>A DLL that names <paramref name="mark"/> in ASCII, padded to the size the marks are read at.</summary>
    public static byte[] Carrying(string mark) => Marked(Encoding.ASCII.GetBytes(mark));

    private static byte[] Marked(byte[] mark)
    {
        byte[] stub = GameFolders.Dll();
        byte[] file = new byte[MarkedSize + stub.Length + mark.Length];
        stub.CopyTo(file, 0);
        mark.CopyTo(file, MarkedSize);
        return file;
    }
}
