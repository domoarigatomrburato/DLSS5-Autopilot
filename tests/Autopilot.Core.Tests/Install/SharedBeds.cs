using System.Text;
using Autopilot.Core.Executables;
using Autopilot.Core.Gpu;
using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.Library.Stores;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>
/// Transaction beds with the places outside the game folder: an in-memory registry for the current user and one for the
/// machine, and the layer folder in the bed's data folder. Layer and graphics preference steps as tests write them.
/// </summary>
internal static class SharedBeds
{
    public const string VulkanKey = @"Software\Khronos\Vulkan\ImplicitLayers";
    public const string OpenXrKey = @"Software\Khronos\OpenXR\1\ApiLayers\Implicit";
    public const string PreferenceKey = @"Software\Microsoft\DirectX\UserGpuPreferences";

    public static readonly byte[] Dll64 = GameFolders.Dll("ReShade64.dll from the setup");
    public static readonly byte[] Dll32 = GameFolders.Dll("ReShade32.dll from the setup");

    /// <summary>A bed holding <paramref name="files"/>, with empty registries and the layer folder in its data folder.</summary>
    public static TransactionBed New(params IEnumerable<TreeFile> files)
    {
        TransactionBed bed = new(files);
        bed.Shared = new SharedPlaces(new FakeUserRegistry(), new FakeRegistry(), Path.Join(bed.DataRoot, "reshade-vulkan"));
        return bed;
    }

    /// <summary>An empty game folder with registries and no layer folder.</summary>
    public static TransactionBed WithoutLayerFolder() => new() { Shared = new SharedPlaces(new FakeUserRegistry(), new FakeRegistry(), null) };

    /// <summary>A bed holding the game's executable.</summary>
    public static TransactionBed NewGame() => New(TreeFile.Bytes("Game.exe", GameFolders.Dll("the game")));

    public static FakeUserRegistry User(this TransactionBed bed) => (FakeUserRegistry)bed.Shared!.User;

    public static FakeRegistry Machine(this TransactionBed bed) => (FakeRegistry)bed.Shared!.Machine;

    public static string LayerFolder(this TransactionBed bed) => bed.Shared!.LayerFolder!;

    /// <summary>A file of the layer folder.</summary>
    public static string LayerFile(this TransactionBed bed, string name) => Path.Join(bed.LayerFolder(), name);

    /// <summary>The layer step, its DLL and manifest as ReShade's setup holds them.</summary>
    public static PlanStep Layer(RegisteredLayer layer, byte[]? library = null)
    {
        byte[] dll = library ?? (layer == RegisteredLayer.VulkanLayer32 ? Dll32 : Dll64);
        return new RegisterLayer(layer, Plans.Facts(dll), new GeneratedSource(dll), SetupManifest(layer));
    }

    /// <summary>The manifest of <paramref name="layer"/> as ReShade's setup ships it.</summary>
    public static byte[] SetupManifest(RegisteredLayer layer) => Encoding.UTF8.GetBytes(layer switch
    {
        RegisteredLayer.OpenXr => /*lang=json,strict*/ """{ "file_format_version": "1.0.0", "api_layer": { "name": "XR_APILAYER_reshade", "library_path": "ReShade64.dll", "api_version": "1.0", "implementation_version": "1", "description": "crosire's ReShade post-processing injector", "disable_environment": "DISABLE_RESHADE" } }""",
        RegisteredLayer.VulkanLayer32 => /*lang=json,strict*/ """{ "file_format_version": "1.0.0", "layer": { "name": "VK_LAYER_reshade", "type": "GLOBAL", "library_path": "ReShade32.dll", "api_version": "1.3.0", "implementation_version": "1", "description": "crosire's ReShade post-processing injector", "disable_environment": { "DISABLE_RESHADE": "1" } } }""",
        _ => /*lang=json,strict*/ """{ "file_format_version": "1.0.0", "layer": { "name": "VK_LAYER_reshade", "type": "GLOBAL", "library_path": "ReShade64.dll", "api_version": "1.3.0", "implementation_version": "1", "description": "crosire's ReShade post-processing injector", "disable_environment": { "DISABLE_RESHADE": "1" } } }""",
    });

    /// <summary>The graphics preference step of <paramref name="program"/>, on a PC with an Intel GPU beside the NVIDIA card.</summary>
    public static PlanStep Preference(string program = "Game.exe") => new SetGraphicsPreference(program, GpuVendor.Intel);

    /// <summary>A step placing a <c>dxgi.dll</c>: a file of the game folder beside the shared steps.</summary>
    public static PlanStep Dxgi(byte[]? content = null) => Plans.Place("dxgi.dll", content ?? Dll32);

    /// <summary>A plan of one group for a game of <paramref name="bitness"/>.</summary>
    public static InstallPlan Plan(this TransactionBed bed, Bitness bitness, params PlanStep[] steps)
    {
        InstallPlan plan = bed.Plan(RouteId.Native, steps);
        return plan with { Header = plan.Header with { Game = plan.Header.Game with { Bitness = bitness } } };
    }

    /// <summary>The program's full path in the bed's game folder: the graphics preference's value name.</summary>
    public static string Program(this TransactionBed bed, string program = "Game.exe") => bed.Tree.PathOf(program);

    /// <summary>Sets the program's graphics preference value as the person or Windows left it.</summary>
    public static void SetPreference(this TransactionBed bed, string value, string program = "Game.exe") =>
        _ = bed.User().Set(PreferenceKey, bed.Program(program), value);
}
