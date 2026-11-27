using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>What a fresh install does to a game folder: the files it writes and the record naming them.</summary>
[TestClass]
public sealed class TransactionTests(TestContext testContext)
{
    private static readonly byte[] _dxgi = GameFolders.Dll("ours");
    private static readonly byte[] _shader = GameFolders.Dll("our shader");

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_fresh_install_writes_the_planned_files_and_an_installed_record()
    {
        using TransactionBed bed = new(GameFolders.Empty);

        InstallCommitted committed = await bed.CommitAsync(bed.Plan(RouteId.Native, Plans.Place("dxgi.dll", _dxgi), Plans.Place("Shaders/a.fx", _shader, EntryClass.Config)), Ct);

        AssertFresh(bed, committed, await bed.RecordAsync(Ct));
    }

    [TestMethod]
    public async Task A_folder_already_there_is_not_recorded_as_created()
    {
        using TransactionBed bed = new(GameFolders.Empty);
        _ = bed.Tree.Folder("Shaders");

        _ = await bed.CommitAsync(bed.Plan(RouteId.Native, new CreateFolder("Shaders"), new CreateFolder("Textures/Big")), Ct);

        Assert.AreEqual("Textures|Textures/Big", string.Join("|", (await bed.RecordAsync(Ct)).CreatedFolders));
    }

    private static void AssertFresh(TransactionBed bed, InstallCommitted committed, InstallRecord record)
    {
        Assert.IsTrue(bed.Holds("dxgi.dll", _dxgi) && bed.Holds("Shaders/a.fx", _shader));
        Assert.AreEqual(RecordState.Installed, record.State);
        Assert.IsNull(record.Previous);
        Assert.AreEqual("Shaders", string.Join("|", record.CreatedFolders));
        Assert.AreEqual(new RecordEntry("dxgi.dll", EntryClass.Component, EntryState.Written, Plans.Facts(_dxgi), null), record.Entries[0]);
        Assert.AreEqual(new RecordEntry("Shaders/a.fx", EntryClass.Config, EntryState.Written, Plans.Facts(_shader), null), record.Entries[1]);
        Assert.AreEqual("installed native", committed.Notes.Single());
        Assert.IsFalse(committed.Recovered);
        Assert.IsNull(bed.Note);
    }
}
