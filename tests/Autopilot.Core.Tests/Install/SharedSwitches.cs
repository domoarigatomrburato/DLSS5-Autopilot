using System.Text.Json.Nodes;
using Autopilot.Core.Executables;
using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Storage;
using Autopilot.Core.Tests.TestBeds;
using static Autopilot.Core.Tests.Install.SharedBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>
/// An install X with every shared part (its proxy, both layers, the game's graphics preference beside a token of Windows')
/// and an update Y on another route that keeps the Vulkan layer and drops the OpenXR layer and the graphics preference.
/// </summary>
internal static class SharedSwitches
{
    public const string Windows = "SwapEffectUpgradeEnable=1;";

    private static readonly byte[] _xDxgi = GameFolders.Dll("X's dxgi");
    private static readonly byte[] _yDxgi = GameFolders.Dll("Y's dxgi");

    /// <summary>A bed with X installed and committed.</summary>
    public static async Task<TransactionBed> InstalledAsync(CancellationToken cancellationToken)
    {
        TransactionBed bed = NewGame();
        bed.SetPreference(Windows);
        _ = await bed.CommitAsync(X(bed), cancellationToken);
        return bed;
    }

    /// <summary>A bed with nothing installed, the game's graphics preference holding Windows' token.</summary>
    public static TransactionBed Fresh()
    {
        TransactionBed bed = NewGame();
        bed.SetPreference(Windows);
        return bed;
    }

    /// <summary>A bed with the Vulkan layer alone installed, the game's graphics preference holding Windows' token.</summary>
    public static async Task<TransactionBed> VulkanOnlyAsync(CancellationToken cancellationToken)
    {
        TransactionBed bed = Fresh();
        _ = await bed.CommitAsync(bed.Plan(Bitness.Bits64, Dxgi(_xDxgi), Layer(RegisteredLayer.VulkanLayer64)), cancellationToken);
        return bed;
    }

    public static InstallPlan X(TransactionBed bed) =>
        bed.Plan(Bitness.Bits64, Dxgi(_xDxgi), Layer(RegisteredLayer.VulkanLayer64), Layer(RegisteredLayer.OpenXr), Preference());

    /// <summary>X again with a newer proxy: an update that keeps every shared part.</summary>
    public static InstallPlan Newer(TransactionBed bed) =>
        bed.Plan(Bitness.Bits64, Dxgi(_yDxgi), Layer(RegisteredLayer.VulkanLayer64), Layer(RegisteredLayer.OpenXr), Preference());

    public static InstallPlan Y(TransactionBed bed)
    {
        InstallPlan plan = bed.Plan(Bitness.Bits64, Dxgi(_yDxgi), Layer(RegisteredLayer.VulkanLayer64));
        return plan with { Header = plan.Header with { Route = RouteId.OptiScaler } };
    }

    /// <summary>The steps a whole run of Y over X makes.</summary>
    public static async Task<IReadOnlyList<WriteStep>> MadeStepsAsync(CancellationToken cancellationToken)
    {
        using TransactionBed bed = await InstalledAsync(cancellationToken);
        var made = CrashPoints.After(int.MaxValue);
        bed.Steps = made;
        _ = await bed.CommitAsync(Y(bed), cancellationToken);
        return made.Made;
    }

    /// <summary>The first registry step of a run of Y over X: the commit's release of the OpenXR layer.</summary>
    public static async Task<int> FirstReleaseAsync(CancellationToken cancellationToken) =>
        (await MadeStepsAsync(cancellationToken)).ToList().FindIndex(step => step.Kind is WriteKind.SetRegistryValue or WriteKind.DeleteRegistryValue);

    /// <summary>
    /// Installs <paramref name="plan"/> over <paramref name="bed"/> with the drive filling at its commit's save, found in a whole run over a
    /// twin bed set up as <paramref name="bed"/> was, by <paramref name="twinOf"/>. The commit can be followed by releases, so the
    /// save is found by what it wrote, not by its place before the held files' deletes.
    /// </summary>
    public static async Task<InstallFailed> FailAtCommitAsync(TransactionBed bed, Func<CancellationToken, Task<TransactionBed>> twinOf, Func<TransactionBed, InstallPlan> plan, CancellationToken cancellationToken)
    {
        using TransactionBed twin = await twinOf(cancellationToken);
        CommitWatch watch = new();
        twin.Steps = watch;
        _ = await twin.CommitAsync(plan(twin), cancellationToken);
        watch.Before(new(WriteKind.SaveRecord, "the run's end"));
        bed.Steps = DiskFullPoints.At(watch.Commit);
        return await bed.InstallAsAsync<InstallFailed>(plan(bed), cancellationToken);
    }

    /// <summary>Installs Y with a crash after <paramref name="survives"/> steps; whether Y's record was committed.</summary>
    public static async Task<bool> CrashYAsync(TransactionBed bed, int survives, CancellationToken cancellationToken)
    {
        bed.Steps = CrashPoints.After(survives);
        _ = await bed.InstallAsync(Y(bed), cancellationToken);
        return await bed.RecordAsync(cancellationToken) is { State: RecordState.Installed, Route: RouteId.OptiScaler };
    }

    /// <summary>Checks Y is committed and finished: the OpenXR layer and the graphics preference released, the Vulkan layer kept.</summary>
    public static void AssertY(TransactionBed bed, InstallRecord record, string at)
    {
        Assert.IsTrue(record is { State: RecordState.Installed, Route: RouteId.OptiScaler, Releasing: null, Registrations: [{ Layer: RegisteredLayer.VulkanLayer64 }], GraphicsPreferences: [] }, at);
        Assert.IsNull(bed.Registered(RegisteredLayer.OpenXr), at);
        Assert.AreEqual(0, bed.Registered(RegisteredLayer.VulkanLayer64), at);
        Assert.AreEqual(Windows, bed.PreferenceOf(), at);
        Assert.AreEqual("ReShade64.dll|ReShade64.json", bed.LayerFiles(), at);
        Assert.IsTrue(bed.Holds("dxgi.dll", _yDxgi), at);
    }
}

/// <summary>Finds the commit's save in a run: the first record save after which the record says it is installed.</summary>
internal sealed class CommitWatch : IWriteSteps
{
    private WriteStep? _last;
    private int _count;

    /// <summary>The commit's save, counted from 1; 0 until it is seen.</summary>
    public int Commit { get; private set; }

    public void Before(WriteStep step)
    {
        if (Commit == 0 && _last is { Kind: WriteKind.SaveRecord } last && (string?)JsonNode.Parse(File.ReadAllBytes(last.Path))?["state"] == "installed")
        {
            Commit = _count;
        }

        _last = step;
        _count++;
    }
}
