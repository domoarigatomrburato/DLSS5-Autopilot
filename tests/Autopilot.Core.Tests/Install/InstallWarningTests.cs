using Autopilot.Core.Executables;
using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>What the person is told before an install, which does not stop it (routes.md, "Pre-install checks and warnings").</summary>
[TestClass]
public sealed class InstallWarningTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task The_warnings_name_the_launcher_the_other_hooks_the_anti_cheat_and_the_re_engine()
    {
        using RefusalBed bed = new(
            TreeFile.Bytes("GTAVLauncher.exe", GameFolders.Dll()),
            TreeFile.Bytes("GTA5.exe", GameFolders.Dll()),
            TreeFile.Bytes("somebody.addon64", GameFolders.Dll()),
            TreeFile.Bytes("plain.addon", GameFolders.Dll()),
            TreeFile.Bytes("renodx-dlss5.addon64", GameFolders.Dll()),
            TreeFile.Bytes("BEService_x64.exe", GameFolders.Dll()));
        string[] candidates = [bed.Tree.PathOf("GTAVLauncher.exe"), bed.Tree.PathOf("GTA5.exe")];

        InstallCheck check = await bed.CheckAsync(
            bed.Request(RouteId.Native) with { Game = bed.Game with { Executable = candidates[0], Candidates = candidates }, Facts = RefusalGames.Facts with { IsReEngine = true } },
            Ct);

        Assert.HasCount(4, check.Warnings, string.Join("\n", check.Warnings));
        Assert.StartsWith("GTAVLauncher.exe looks like a launcher, not the game; GTA5.exe looks like the real one.", check.Warnings[0]);
        Assert.StartsWith("Another DLSS hook is in the folder (plain.addon, somebody.addon64)", check.Warnings[1]);
        Assert.StartsWith("BattlEye is installed with this game.", check.Warnings[2]);
        Assert.StartsWith("This looks like a Capcom RE Engine game (re_chunk_000.pak is in the folder)", check.Warnings[3]);
    }

    [TestMethod]
    public async Task A_launcher_alone_is_still_said()
    {
        using RefusalBed bed = new(TreeFile.Bytes("Launcher.exe", GameFolders.Dll()));

        InstallCheck check = await bed.CheckAsync(bed.Request(RouteId.Native) with { Game = bed.Game with { Executable = bed.Tree.PathOf("Launcher.exe"), Candidates = [bed.Tree.PathOf("Launcher.exe")] } }, Ct);

        Assert.StartsWith("Launcher.exe looks like a launcher, not the game. If nothing changes", check.Warnings.Single());
    }

    [TestMethod]
    public async Task The_previous_installs_own_add_ons_are_not_another_hook()
    {
        using RefusalBed bed = new(TreeFile.Bytes("gitc-uplift.addon64", GameFolders.Dll()));
        Recorded.Plant(bed.Tree.Root, Recorded.Install("", Recorded.Ours("gitc-uplift.addon64")));

        Assert.IsEmpty((await bed.CheckAsync(bed.Request(RouteId.Native), Ct)).Warnings);
    }

    [TestMethod]
    public async Task The_warnings_carry_the_routes_driver_note_and_its_reliability()
    {
        using RefusalBed bed = new();
        RouteOption uplift = new(RouteId.Uplift, true, [], RouteReliability.Experimental, [new RouteMessage("RouteDriverTryStandalone", [])]);

        InstallCheck check = await bed.CheckAsync(bed.Request(RouteId.Uplift) with { Route = uplift }, Ct);

        Assert.HasCount(2, check.Warnings);
        Assert.StartsWith("gitc-uplift is experimental on this game", check.Warnings[1]);
    }

    [TestMethod]
    public async Task A_beta_route_says_it_works_in_many_games_not_all()
    {
        using RefusalBed bed = new();

        InstallCheck check = await bed.CheckAsync(bed.Request(RouteId.Bridge) with { Route = RefusalGames.Option(RouteId.Bridge, RouteReliability.Beta) }, Ct);

        Assert.StartsWith("bridge is beta on this game", check.Warnings.Single());
    }

    [TestMethod]
    public async Task An_agility_sdk_this_windows_lacks_is_said()
    {
        using RefusalBed bed = new();
        AgilityUnmet unmet = new(bed.Tree.PathOf("Game.exe"), 717, @".\D3D12\", 614);

        InstallCheck check = await bed.CheckAsync(bed.Request(RouteId.Native) with { Agility = unmet }, Ct);

        Assert.StartsWith("Game.exe asks Windows for the Direct3D 12 Agility SDK version 717", check.Warnings.Single());
    }

    [TestMethod]
    [DataRow(RecordState.Installing, "rolled back")]
    [DataRow(RecordState.Removing, "removed")]
    public async Task An_interrupted_operation_is_said_to_be_finished_first(RecordState state, string how)
    {
        using RefusalBed bed = new();
        PlantOpen(bed, state);

        InstallCheck check = await bed.CheckAsync(bed.Request(RouteId.Native), Ct);

        check.AssertAllowed();
        Assert.StartsWith("An earlier install or uninstall in this folder was interrupted: it is " + how + " first", check.Warnings[0]);
    }

    // A record left open by an install or an uninstall that did not finish.
    private static void PlantOpen(RefusalBed bed, RecordState state) => Recorded.Plant(bed.Tree.Root, Recorded.Install() with { State = state });
}
