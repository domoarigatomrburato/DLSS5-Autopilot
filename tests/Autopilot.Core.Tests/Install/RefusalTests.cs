using System.Text.Json;
using System.Text.Json.Nodes;
using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>What the transaction refuses before writing anything: a record it cannot trust, and paths outside the game folder.</summary>
[TestClass]
public sealed class RefusalTests(TestContext testContext)
{
    private static readonly byte[] _ours = GameFolders.Dll("ours");

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    [DataRow("{ damaged")]
    [DataRow("newer")]
    public async Task A_record_it_cannot_read_refuses_the_install_with_nothing_written(string record)
    {
        using TransactionBed bed = new(GameFolders.Empty);
        Plant(bed, record == "newer" ? Newer() : record);
        TreeSnapshot before = bed.Snapshot();

        InstallRefused refused = await bed.InstallAsAsync<InstallRefused>(OneFile(bed, "a.dll"), Ct);

        AssertRefused(InstallRefusal.RecordUnreadable, refused, before, bed);
    }

    [TestMethod]
    public async Task A_record_whose_game_folder_is_outside_the_plans_refuses_the_install()
    {
        using TransactionBed bed = new(GameFolders.Empty) { InstallFolder = "Sub" };
        Recorded.Plant(bed.Tree.PathOf("Sub"), Recorded.Install("Sub"));
        TreeSnapshot before = bed.Snapshot();

        InstallRefused refused = await bed.InstallAsAsync<InstallRefused>(OneFile(bed, "Sub/a.dll") with { GameFolder = bed.Tree.PathOf("Sub") }, Ct);

        AssertRefused(InstallRefusal.RecordOutsideGameFolder, refused, before, bed);
    }

    [TestMethod]
    [DataRow("../a.dll")]
    [DataRow("/a.dll")]
    [DataRow("Link/a.dll")]
    public async Task A_plan_path_outside_the_game_folder_refuses_the_install(string path)
    {
        using TransactionBed bed = new(GameFolders.Empty);
        using GameTree elsewhere = LinkedElsewhere(bed);
        TreeSnapshot before = bed.Snapshot();

        InstallRefused refused = await bed.InstallAsAsync<InstallRefused>(OneFile(bed, path), Ct);

        AssertRefused(InstallRefusal.PathOutsideGameFolder, refused, before, bed);
    }

    private static InstallPlan OneFile(TransactionBed bed, string path) => bed.Plan(RouteId.Native, Plans.Place(path, _ours));

    private static void Plant(TransactionBed bed, string record) => TestFiles.Plant(bed.Tree.PathOf(KnownFiles.InstallRecord), record);

    // A folder outside the game's, reached through a link inside it.
    private static GameTree LinkedElsewhere(TransactionBed bed)
    {
        GameTree elsewhere = new("Elsewhere");
        _ = Directory.CreateSymbolicLink(bed.Tree.PathOf("Link"), elsewhere.Root);
        return elsewhere;
    }

    private static string Newer()
    {
        JsonObject json = JsonSerializer.SerializeToNode(Recorded.Install(), InstallJsonContext.Default.InstallRecord)!.AsObject();
        json["version"] = 2;
        return json.ToJsonString();
    }

    private static void AssertRefused(InstallRefusal reason, InstallRefused refused, TreeSnapshot before, TransactionBed bed)
    {
        Assert.AreEqual(reason, refused.Reason);
        before.AssertSame(bed.Snapshot());
        Assert.IsNull(bed.Note);
    }
}
