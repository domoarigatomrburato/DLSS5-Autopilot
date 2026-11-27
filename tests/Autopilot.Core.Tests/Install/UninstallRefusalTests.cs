using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>Uninstall refused before it changes anything, and a record entry outside the game folder it never follows.</summary>
[TestClass]
public sealed class UninstallRefusalTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_running_game_refuses_the_uninstall_and_nothing_changes()
    {
        using TransactionBed bed = new(Switches.GameFiles);
        _ = await bed.CommitAsync(Switches.X(bed), Ct);
        TreeSnapshot installed = bed.Snapshot();
        _ = bed.Processes.Run(bed.Tree.PathOf("Game.exe"));

        UninstallRefused refused = await bed.UninstallAsAsync<UninstallRefused>(Ct);

        Assert.AreEqual(UninstallRefusal.GameRunning, refused.Reason);
        installed.AssertSame(bed.Snapshot());
    }

    [TestMethod]
    public async Task A_record_from_a_newer_build_refuses_the_uninstall()
    {
        using TransactionBed bed = new(TreeFile.Text(KnownFiles.InstallRecord, /*lang=json,strict*/ "{\"version\": 99}"), TreeFile.Text("dlss5-feed.addon64", "ours?"));
        TreeSnapshot before = bed.Snapshot();

        UninstallRefused refused = await bed.UninstallAsAsync<UninstallRefused>(Ct);

        Assert.IsTrue(refused is { Reason: UninstallRefusal.RecordUnreadable, Problem: RecordProblem.Newer });
        before.AssertSame(bed.Snapshot());
    }

    [TestMethod]
    public async Task A_record_naming_a_game_folder_outside_the_one_given_refuses_the_uninstall()
    {
        using TransactionBed bed = new(GameFolders.Empty);
        Recorded.Plant(bed.Tree.PathOf("Binaries/Win64"), Recorded.Install("Binaries/Win64", Recorded.Ours("Binaries/Win64/a.dll")));

        UninstallOutcome outcome = await bed.UninstallAsync("Binaries", "Binaries/Win64", Ct);

        Assert.AreEqual(UninstallRefusal.RecordOutsideGameFolder, (outcome as UninstallRefused)?.Reason);
    }

    [TestMethod]
    public async Task An_entry_reaching_outside_the_game_folder_is_never_followed_and_lets_the_record_go()
    {
        using TransactionBed bed = new(GameFolders.Empty);
        using GameTree elsewhere = RecordedThroughLink(bed);

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.IsTrue(finished is { Complete: true, Lines: ["ignored: Link/a.dll: outside the game folder"] });
        Assert.IsNull(await bed.FindAsync(Ct));
        Assert.IsTrue(File.Exists(elsewhere.PathOf("a.dll")));
    }

    // A record naming Link/a.dll, where Link leads to a folder outside the game's holding someone else's a.dll.
    private static GameTree RecordedThroughLink(TransactionBed bed)
    {
        GameTree elsewhere = new("Elsewhere");
        TestFiles.Plant(elsewhere.PathOf("a.dll"), "theirs");
        _ = Directory.CreateSymbolicLink(bed.Tree.PathOf("Link"), elsewhere.Root);
        Recorded.Plant(bed.Tree.Root, Recorded.Install("", Recorded.Ours("Link/a.dll")));
        return elsewhere;
    }
}
