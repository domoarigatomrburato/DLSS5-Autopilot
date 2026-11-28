using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>Installs for uninstall tests to take apart, and what a test reads back.</summary>
internal static class UninstallBed
{
    /// <summary>Our component's bytes.</summary>
    public static readonly byte[] Ours = GameFolders.Dll("ours");

    /// <summary>A newer file the game shipped since.</summary>
    public static readonly byte[] Updated = GameFolders.Dll("the game's file, updated");

    /// <summary>Our config file's bytes.</summary>
    public static readonly byte[] OurIni = "[GENERAL]\r\nours=1\r\n"u8.ToArray();

    /// <summary>A bed holding <paramref name="files"/>, with a plan of <paramref name="steps"/> installed over them on the native route.</summary>
    public static async Task<TransactionBed> InstalledAsync(TreeFile[] files, CancellationToken cancellationToken, params PlanStep[] steps)
    {
        TransactionBed bed = new(files);
        _ = await bed.CommitAsync(bed.Plan(RouteId.Native, steps), cancellationToken);
        return bed;
    }

    /// <summary>A bed whose empty folder has our <paramref name="paths"/> installed in it.</summary>
    public static async Task<TransactionBed> OursAsync(CancellationToken cancellationToken, params string[] paths)
    {
        TransactionBed bed = new(GameFolders.Empty);
        await bed.InstallOursAsync(cancellationToken, paths);
        return bed;
    }

    /// <summary>Installs our <paramref name="paths"/> in the bed, which the test expects to commit.</summary>
    public static Task InstallOursAsync(this TransactionBed bed, CancellationToken cancellationToken, params string[] paths) =>
        bed.CommitAsync(bed.OursPlan(paths), cancellationToken);

    /// <summary>A plan placing our <paramref name="paths"/> on the native route.</summary>
    public static InstallPlan OursPlan(this TransactionBed bed, params string[] paths) =>
        bed.Plan(RouteId.Native, [.. paths.Select(path => PlanBeds.Place(path, Ours))]);

    /// <summary>The game's read-only <c>dxgi.dll</c> and the rest of <see cref="Switches.GameFiles"/>, with ours over <c>dxgi.dll</c> and its backup beside.</summary>
    public static Task<TransactionBed> SwappedAsync(CancellationToken cancellationToken) =>
        InstalledAsync(Switches.GameFiles, cancellationToken, PlanBeds.Place("dxgi.dll", Ours));

    /// <summary>A bed with <see cref="Switches.X"/> installed over <see cref="Switches.GameFiles"/>.</summary>
    public static async Task<TransactionBed> XInstalledAsync(CancellationToken cancellationToken)
    {
        TransactionBed bed = new(Switches.GameFiles);
        _ = await bed.CommitAsync(Switches.X(bed), cancellationToken);
        return bed;
    }

    /// <summary>Checks the uninstall said every one of <paramref name="lines"/>, among others.</summary>
    public static void AssertSaid(UninstallFinished finished, params string[] lines) => CollectionAssert.IsSubsetOf(lines, finished.Lines.ToArray());

    /// <summary>Checks the uninstall said a line starting with <paramref name="start"/>.</summary>
    public static void AssertSaidStarting(UninstallFinished finished, string start) =>
        Assert.IsTrue(finished.Lines.Any(line => line.StartsWith(start, StringComparison.Ordinal)), string.Join(" / ", finished.Lines));
}
