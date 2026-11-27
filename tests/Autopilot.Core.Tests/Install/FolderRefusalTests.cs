using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Storage;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>The refusals the folder decides, per install.md's "Refusal checks": read-only queries, every blocker reported, each saying what to do.</summary>
[TestClass]
public sealed class FolderRefusalTests(TestContext testContext)
{
    private static readonly RouteSettings _quantMotion = new() { MotionProvider = Core.Components.Catalog.MotionProvider.QuantMotion };

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_plain_install_meets_no_blocker_and_no_warning()
    {
        using RefusalBed bed = new(TreeFile.Bytes("Game.exe", GameFolders.Dll()));

        InstallCheck check = await bed.CheckAsync(bed.Request(RouteId.Native), Ct);

        check.AssertAllowed();
        Assert.IsEmpty(check.Warnings);
    }

    [TestMethod]
    public async Task A_missing_install_folder_is_refused()
    {
        using RefusalBed bed = new();

        RefusalRequest request = bed.Request(RouteId.Native);

        InstallCheck check = await bed.CheckAsync(request with { Plan = request.Plan with { InstallFolder = bed.Tree.PathOf("Binaries") } }, Ct);

        check.AssertSays(BlockerKind.FolderMissing, "does not exist. Check the game's folder");
    }

    [TestMethod]
    public async Task An_unwritable_folder_is_refused_before_any_lock_is_asked()
    {
        using RefusalBed bed = new(TreeFile.Bytes("dxgi.dll", GameFolders.Dll()));
        _ = bed.Access.Deny(bed.Tree.Root);
        using FileStream launcher = bed.Hold("dxgi.dll");

        InstallCheck check = await bed.CheckAsync(bed.Request(RouteId.Native, placed: "dxgi.dll"), Ct);

        Assert.AreEqual("FolderDenied", check.Kinds());
        Assert.Contains("run the tool as administrator", check.Blockers[0].Text);
    }

    [TestMethod]
    public async Task An_xbox_folder_gets_the_xbox_apps_answer()
    {
        using RefusalBed bed = new();
        _ = bed.Access.Deny(bed.Tree.Root);

        InstallCheck check = await bed.CheckAsync(bed.Request(RouteId.Native) with { Game = bed.Game with { Folder = @"D:\XboxGames\Game\Content" } }, Ct);

        check.AssertSays(BlockerKind.StoreFolder, "'Enable mods' in the Xbox app");
    }

    [TestMethod]
    public async Task A_running_game_is_refused_and_asked_again_before_the_transaction()
    {
        using RefusalBed bed = new();
        InstallRefusals refusals = bed.Refusals();
        Assert.IsNull(refusals.GameRunning(bed.Tree.Root, bed.Tree.Root));

        _ = bed.Processes.Run(bed.Tree.PathOf(@"bin\Game.exe"));

        (await bed.CheckAsync(bed.Request(RouteId.Native), Ct)).AssertSays(BlockerKind.GameRunning, "Game.exe is running. Close the game first");
        Assert.AreEqual(BlockerKind.GameRunning, refusals.GameRunning(bed.Tree.Root, bed.Tree.Root)?.Kind);
    }

    [TestMethod]
    public async Task A_file_another_program_holds_is_named()
    {
        using RefusalBed bed = new(TreeFile.Bytes("dxgi.dll", GameFolders.Dll()));
        using FileStream launcher = bed.Hold("dxgi.dll");

        InstallCheck check = await bed.CheckAsync(bed.Request(RouteId.Native, placed: "dxgi.dll"), Ct);

        check.AssertSays(BlockerKind.Locked, "dxgi.dll is held open by another program");
    }

    [TestMethod]
    public async Task A_damaged_record_is_refused_with_uninstall_first()
    {
        using RefusalBed bed = new();
        TestFiles.Plant(bed.Tree.PathOf("invidia-claritatis.json"), "{ not json");

        InstallCheck check = await bed.CheckAsync(bed.Request(RouteId.Native), Ct);

        check.AssertSays(BlockerKind.Record, "Uninstall first, then install");
    }

    [TestMethod]
    public async Task A_drive_without_room_names_the_drive_what_is_free_and_what_is_needed()
    {
        using RefusalBed bed = new() { Drive = new("G:", 1024) };

        InstallCheck check = await bed.CheckAsync(bed.Request(RouteId.Native), Ct);

        Assert.AreEqual("Not enough space on G:: 1 KB free, and the install needs 2 MB. Free some space on it, then install again.", check.Only(BlockerKind.FreeSpace));
    }

    [TestMethod]
    public async Task A_drive_whose_room_is_not_known_is_never_refused()
    {
        using RefusalBed bed = new() { Drive = new DriveSpace("G:", null) };

        (await bed.CheckAsync(bed.Request(RouteId.Native), Ct)).AssertAllowed();
    }

    [TestMethod]
    public async Task Several_blockers_are_all_reported()
    {
        using RefusalBed bed = new(TreeFile.Bytes("Game.exe", GameFolders.Dll())) { Drive = new("G:", 1024) };
        _ = bed.Processes.Run(bed.Tree.PathOf("Game.exe"));

        RefusalRequest request = bed.Request(RouteId.Feeder, _quantMotion);

        Assert.AreEqual("MotionProvider, GameRunning, FreeSpace", (await bed.CheckAsync(request, Ct)).Kinds());
    }

    [TestMethod]
    public async Task A_check_writes_nothing_even_when_it_refuses()
    {
        using RefusalBed bed = new(TreeFile.Bytes("Game.exe", GameFolders.Dll()), TreeFile.Bytes("dxgi.dll", GameFolders.Dll())) { Drive = new("G:", 1024) };
        TreeSnapshot before = bed.Bed.Snapshot();

        Assert.IsFalse((await bed.CheckAsync(bed.Request(RouteId.Native, placed: "dxgi.dll"), Ct)).CanInstall);

        before.AssertSame(bed.Bed.Snapshot());
    }
}
