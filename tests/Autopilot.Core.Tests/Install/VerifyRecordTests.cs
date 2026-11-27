using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>"Verify" of a folder whose record cannot be read, or that has none.</summary>
[TestClass]
public sealed class VerifyRecordTests
{
    [TestMethod]
    public async Task A_damaged_record_is_said_as_such_and_no_record_as_none()
    {
        using TransactionBed damaged = new(TreeFile.Text(KnownFiles.InstallRecord, "not json"));
        using TransactionBed none = new(GameFolders.Empty);

        RecordVerification unreadable = await VerifyTests.VerifyAsync(damaged);
        RecordVerification nothing = await VerifyTests.VerifyAsync(none);

        Assert.IsTrue(unreadable is { Record: VerifiedRecord.Unreadable, Problem: RecordProblem.Damaged, Entries.Count: 0 });
        Assert.IsTrue(nothing is { Record: VerifiedRecord.None, Entries.Count: 0 });
    }

    [TestMethod]
    public async Task An_open_records_entries_not_written_yet_are_said_as_such_and_not_as_gone()
    {
        using TransactionBed bed = OpenRecord();

        RecordVerification verified = await VerifyTests.VerifyAsync(bed);

        Assert.IsTrue(verified is { State: InstallState.Installing, AllOk: true });
        Assert.AreEqual(VerifiedFile.NotWritten, verified.Entries.Single().File);
    }

    // An install part way, its one file not written yet.
    private static TransactionBed OpenRecord()
    {
        TransactionBed bed = new(GameFolders.Empty);
        Recorded.Plant(bed.Tree.Root, Recorded.Install("", Recorded.Ours("a.dll") with { State = EntryState.Pending }) with { State = RecordState.Installing });
        return bed;
    }
}
