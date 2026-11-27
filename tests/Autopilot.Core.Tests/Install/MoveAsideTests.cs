using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>Moving files aside over an install: one already aside stays so, and one of ours goes at commit.</summary>
[TestClass]
public sealed class MoveAsideTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_file_already_aside_stays_aside_and_our_file_moved_aside_goes_at_commit()
    {
        using TransactionBed bed = await Switches.InstalledAsync(Ct);

        _ = await bed.CommitAsync(AsideAgain(bed), Ct);

        AssertAside(bed, await bed.EntriesAsync(Ct));
    }

    [TestMethod]
    public async Task Moving_aside_over_an_install_rolled_back_leaves_the_folder_as_it_was()
    {
        using TransactionBed bed = await Switches.InstalledAsync(Ct);
        InstallRecord x = await bed.RecordAsync(Ct);
        TreeSnapshot before = bed.Snapshot();

        _ = await bed.FailAtCommitAsync(AsideAgain(bed), Ct);

        before.AssertSame(bed.Snapshot(), KnownFiles.InstallRecord);
        FullRecord.AssertSame(x, await bed.RecordAsync(Ct));
    }

    private static InstallPlan AsideAgain(TransactionBed bed) => bed.Plan(
        RouteId.Native,
        new KeepEntry("dxgi.dll"),
        new KeepEntry("version.dll"),
        new MoveAside("d3dcompiler_47.dll", BackupKind.Dlss5Off),
        new MoveAside("dlss5-feed.addon64", BackupKind.Dlss5Off));

    private static void AssertAside(TransactionBed bed, string entries)
    {
        Assert.IsFalse(bed.Exists("d3dcompiler_47.dll"));
        Assert.IsTrue(bed.Holds("d3dcompiler_47.dll.dlss5-off", Switches.GameCompiler));
        Assert.IsFalse(bed.Exists("dlss5-feed.addon64") || bed.Exists("dlss5-feed.addon64.dlss5-off"), "our add-on is not the game's: nothing of it is kept");
        Assert.AreEqual("", bed.HeldFiles());
        Assert.AreEqual("dxgi.dll Written|version.dll Written|d3dcompiler_47.dll MovedAside", entries);
    }
}
