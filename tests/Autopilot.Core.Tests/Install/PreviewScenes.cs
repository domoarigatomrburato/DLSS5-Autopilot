using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>The folder and the plan the preview tests show, and what a transaction did to a folder.</summary>
internal static class PreviewScenes
{
    public static readonly byte[] Ours = GameFolders.Dll("ours");

    /// <summary>The executable, the game's own <c>dxgi.dll</c>, and another route's add-on nobody recorded.</summary>
    public static TreeFile[] Folder() =>
    [
        TreeFile.Bytes("Game.exe", GameFolders.Dll()),
        TreeFile.Bytes("dxgi.dll", GameFolders.Dll("the game's")),
        TreeFile.Bytes("dlss5-bridge.addon64", GameFolders.Dll("bridge")),
    ];

    /// <summary>ReShade over the game's <c>dxgi.dll</c> with its ini, then the add-on and the runtime, the bridge's add-on set aside.</summary>
    public static InstallPlan Plan(RefusalBed bed) => bed.Bed.Plan(
        RouteId.Native,
        new PlanGroup("ReShade", [Plans.Place("dxgi.dll", Ours), Plans.Place("ReShade.ini", "[ADDON]\r\n"u8.ToArray(), EntryClass.Config)]),
        new PlanGroup("Add-on", [Plans.Place("renodx-dlss5.addon64", Ours), Plans.Place("nvngx_dlssnr.dll", Ours), new MoveAside("dlss5-bridge.addon64", BackupKind.Backup)]));

    /// <summary><see cref="Plan"/>'s request, on a route that is beta on this game.</summary>
    public static RefusalRequest BetaRequest(RefusalBed bed) => bed.Request(Plan(bed)) with { Route = RefusalGames.Option(RouteId.Native, RouteReliability.Beta) };

    /// <summary>A shader file and a pattern for the files only the download names, beside it.</summary>
    public static InstallPlan PatternPlan(RefusalBed bed) =>
        bed.Bed.Plan(RouteId.Feeder, new PlanGroup("Shaders", [new PlacePattern("reshade-shaders/Shaders/lumenite_*.fxh"), Plans.Place("reshade-shaders/Shaders/DLSS5_Feed.fx", Ours)]));
}
