using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>
/// What is already under a backup name: a stale backup superseded and a leftover not trusted.
/// </summary>
[TestClass]
public sealed class BackupNameTests(TestContext testContext)
{
    private static readonly byte[] _dxgi = GameFolders.Dll("ours");
    private static readonly byte[] _addon = GameFolders.Dll("our add-on");

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_backup_the_game_updated_past_is_superseded_under_a_free_name_and_the_report_says_so()
    {
        using TransactionBed bed = await UpdatedByTheGameAsync(Ct);

        InstallCommitted committed = await bed.CommitAsync(bed.Plan(RouteId.Native, Plans.Place("version.dll", _addon)), Ct);

        AssertSuperseded(bed, committed, await bed.RecordAsync(Ct));
    }

    [TestMethod]
    public async Task A_leftover_under_the_backup_name_is_not_trusted_and_gives_way_to_the_real_backup()
    {
        using TransactionBed bed = new(Switches.WithLeftoverBackup);

        _ = await bed.CommitAsync(bed.Plan(RouteId.Native, Plans.Place("dxgi.dll", _dxgi)), Ct);

        Assert.IsTrue(bed.BackupIsTheOriginal());
    }

    /// <summary>Ours over the game's <c>version.dll</c>, the game's update over ours, and a superseded name already taken.</summary>
    private static async Task<TransactionBed> UpdatedByTheGameAsync(CancellationToken cancellationToken)
    {
        TransactionBed bed = new(Switches.GameFiles);
        _ = await bed.CommitAsync(bed.Plan(RouteId.Native, Plans.Place("version.dll", _dxgi)), cancellationToken);
        TestFiles.Plant(bed.Tree.PathOf("version.dll"), Switches.UpdatedVersion);
        TestFiles.Plant(bed.Tree.PathOf("version.dll.invidia-claritatis-superseded"), "an older one, kept"u8.ToArray());
        return bed;
    }

    private static void AssertSuperseded(TransactionBed bed, InstallCommitted committed, InstallRecord record)
    {
        Assert.IsTrue(bed.Holds("version.dll.invidia-claritatis-superseded.2", Switches.GameVersion), "a taken name is never overwritten");
        Assert.IsTrue(bed.Holds("version.dll.invidia-claritatis-backup", Switches.UpdatedVersion));
        Assert.AreEqual(Plans.Facts(Switches.UpdatedVersion), record.Entries.Single().Original!.Facts);
        Assert.Contains(note => note.Contains("version.dll.invidia-claritatis-superseded.2", StringComparison.Ordinal), committed.Notes);
    }
}
