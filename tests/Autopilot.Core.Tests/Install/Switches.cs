using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>
/// A representative route switch with backups: route X installed over the game's own files, the game then
/// updating one of ours, and route Y installed over that, touching every kind of action.
/// </summary>
internal static class Switches
{
    public static readonly DateTimeOffset GameTime = new(2025, 6, 7, 8, 9, 10, TimeSpan.Zero);

    public static readonly byte[] GameDxgi = GameFolders.Dll("the game's dxgi");
    public static readonly byte[] GameDlss = GameFolders.Dll("the game's dlss");
    public static readonly byte[] GameCompiler = GameFolders.Dll("the game's d3dcompiler");
    public static readonly byte[] GameVersion = GameFolders.Dll("the game's version");
    public static readonly byte[] UpdatedVersion = GameFolders.Dll("the game's version, updated");
    public static readonly byte[] XDxgi = GameFolders.Dll("X's dxgi");
    public static readonly byte[] XFeed = GameFolders.Dll("X's feed add-on");
    public static readonly byte[] XVersion = GameFolders.Dll("X's version");
    public static readonly byte[] YDxgi = GameFolders.Dll("Y's dxgi");
    public static readonly byte[] YAddon = GameFolders.Dll("Y's add-on");
    public static readonly byte[] YDlss = GameFolders.Dll("Y's dlss");
    public static readonly byte[] YVersion = GameFolders.Dll("Y's version");
    public static readonly byte[] YShader = "float4 main() : SV_Target { return 0; }"u8.ToArray();

    /// <summary>The game's own files: a read-only <c>dxgi.dll</c> with an old modified time among them.</summary>
    public static TreeFile[] GameFiles =>
    [
        TreeFile.Bytes("Game.exe", GameFolders.Dll("the game")),
        TreeFile.Bytes("dxgi.dll", GameDxgi) with { ReadOnly = true, Modified = GameTime },
        TreeFile.Bytes("nvngx_dlss.dll", GameDlss) with { Modified = GameTime },
        TreeFile.Bytes("d3dcompiler_47.dll", GameCompiler) with { Modified = GameTime },
        TreeFile.Bytes("version.dll", GameVersion) with { Modified = GameTime },
    ];

    /// <summary>The game's files with a leftover, read-only, under <c>dxgi.dll</c>'s backup name.</summary>
    public static TreeFile[] WithLeftoverBackup => [.. GameFiles, TreeFile.Text("dxgi.dll.invidia-claritatis-backup", "cut short") with { ReadOnly = true }];

    /// <summary>X: its <c>dxgi.dll</c> and <c>version.dll</c> over the game's, its add-on, and the game's compiler moved aside.</summary>
    public static InstallPlan X(TransactionBed bed) => bed.Plan(
        RouteId.Native,
        Plans.Place("dxgi.dll", XDxgi),
        Plans.Place("dlss5-feed.addon64", XFeed),
        Plans.Place("version.dll", XVersion),
        new MoveAside("d3dcompiler_47.dll", BackupKind.Dlss5Off));

    /// <summary>
    /// Y: its <c>dxgi.dll</c> over X's, a new add-on, its <c>nvngx_dlss.dll</c> over the game's, its <c>version.dll</c>
    /// over the game's update, and a shader in a new folder. X's add-on goes, and the compiler comes back.
    /// </summary>
    public static InstallPlan Y(TransactionBed bed) => bed.Plan(
        RouteId.OptiScaler,
        new PlanGroup("ReShade", [Plans.Place("dxgi.dll", YDxgi), Plans.Place("renodx-dlss5.addon64", YAddon)]),
        new PlanGroup("Runtimes", [Plans.Place("nvngx_dlss.dll", YDlss), Plans.Place("version.dll", YVersion), Plans.Place("reshade-shaders/Shaders/y.fx", YShader, EntryClass.Config)]));

    /// <summary>A bed with X installed, the game's update of <c>version.dll</c> over X's, and a leftover under <c>nvngx_dlss.dll</c>'s backup name.</summary>
    public static async Task<TransactionBed> InstalledAsync(CancellationToken cancellationToken)
    {
        TransactionBed bed = new(GameFiles);
        _ = await bed.CommitAsync(X(bed), cancellationToken);
        await File.WriteAllBytesAsync(bed.Tree.PathOf("version.dll"), UpdatedVersion, cancellationToken);
        await File.WriteAllBytesAsync(bed.Tree.PathOf("nvngx_dlss.dll.invidia-claritatis-backup"), "a copy cut short"u8.ToArray(), cancellationToken);
        return bed;
    }

    /// <summary>Checks the folder is byte for byte as X left it, but for the record's modified time, and the record is X's.</summary>
    public static void AssertBackToX(TreeSnapshot before, TransactionBed bed, InstallRecord x, InstallRecord record, string at = "")
    {
        Assert.IsNull(before.Difference(bed.Snapshot(), KnownFiles.InstallRecord), at);
        FullRecord.AssertSame(x, record);
    }

    /// <summary>Checks Y is installed and committed: its record, no held file left, its <c>dxgi.dll</c> in place.</summary>
    public static void AssertY(TransactionBed bed, InstallRecord record, string at = "")
    {
        Assert.IsTrue(record is { State: RecordState.Installed, Route: RouteId.OptiScaler, Previous: null }, at);
        Assert.AreEqual("", bed.HeldFiles(), at);
        Assert.IsTrue(bed.Holds("dxgi.dll", YDxgi), at);
    }
}
