using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>"Verify": the record against the disk, read only, per entry.</summary>
[TestClass]
public sealed class VerifyTests(TestContext testContext)
{
    private static readonly MoveAside _aside = new("d3dcompiler_47.dll", BackupKind.Dlss5Off);

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task An_install_as_written_verifies_ok_with_its_backups_valid()
    {
        using TransactionBed bed = await InstalledAsync(Ct);

        RecordVerification verified = await VerifyAsync(bed);

        Assert.AreEqual(VerifiedRecord.Read, verified.Record);
        Assert.AreEqual(InstallState.Installed, verified.State);
        Assert.IsTrue(verified.AllOk);
        Assert.AreEqual("a.dll Ok |d3dcompiler_47.dll Ok True|dxgi.dll Ok True", Describe(verified));
    }

    [TestMethod]
    public async Task A_file_gone_or_changed_since_and_a_damaged_backup_are_each_said_and_nothing_is_written()
    {
        using TransactionBed bed = await InstalledAsync(Ct);
        bed.Delete("a.dll");
        bed.Write("d3dcompiler_47.dll", UninstallBed.Updated);
        bed.Write("dxgi.dll" + KnownFiles.BackupSuffix, UninstallBed.Updated);
        TreeSnapshot before = bed.Snapshot();

        RecordVerification verified = await VerifyAsync(bed);

        Assert.IsFalse(verified.AllOk);
        Assert.AreEqual("a.dll Missing |d3dcompiler_47.dll Changed True|dxgi.dll Ok False", Describe(verified));
        before.AssertSame(bed.Snapshot());
    }

    [TestMethod]
    public async Task Our_file_replaced_since_is_changed()
    {
        using TransactionBed bed = await InstalledAsync(Ct);
        bed.Write("a.dll", UninstallBed.Updated);

        RecordVerification verified = await VerifyAsync(bed);

        Assert.AreEqual("a.dll Changed |d3dcompiler_47.dll Ok True|dxgi.dll Ok True", Describe(verified));
    }

    [TestMethod]
    public async Task A_file_back_where_the_install_moved_one_aside_is_changed()
    {
        using TransactionBed bed = await InstalledAsync(Ct);
        bed.Write("d3dcompiler_47.dll", Switches.GameCompiler);

        RecordVerification verified = await VerifyAsync(bed);

        Assert.AreEqual("a.dll Ok |d3dcompiler_47.dll Changed True|dxgi.dll Ok True", Describe(verified));
    }

    // a.dll ours, dxgi.dll over the game's, the compiler moved aside.
    private static async Task<TransactionBed> InstalledAsync(CancellationToken cancellationToken)
    {
        TransactionBed bed = await UninstallBed.InstalledAsync(Switches.GameFiles, cancellationToken, PlanBeds.Place("a.dll", UninstallBed.Ours), PlanBeds.Place("dxgi.dll", UninstallBed.Ours), _aside);
        return bed;
    }

    internal static Task<RecordVerification> VerifyAsync(TransactionBed bed) =>
        new InstallVerification(Recorded.Reader()).VerifyAsync(bed.Tree.PathOf(bed.InstallFolder), CancellationToken.None);

    private static string Describe(RecordVerification verified) =>
        string.Join("|", verified.Entries.OrderBy(entry => entry.Path, StringComparer.Ordinal).Select(entry => $"{entry.Path} {entry.File} {entry.BackupValid}"));
}
