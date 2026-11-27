using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Storage;
using Autopilot.Core.Tests.Library.Stores;
using Autopilot.Core.Tests.TestBeds;
using static Autopilot.Core.Tests.Install.SharedBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>The first phase of an install with a layer: a layer file a program holds, and the room on the layer folder's drive.</summary>
[TestClass]
public sealed class SharedRefusalTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_layer_dll_a_running_program_holds_is_named_before_anything_is_written()
    {
        using RefusalBed bed = WithPlaces();
        string dll = bed.Bed.LayerFile("ReShade64.dll");
        TestFiles.Plant(dll, "an older ReShade64.dll");
        using FileStream held = new(dll, FileMode.Open, FileAccess.Read, FileShare.Read);

        InstallCheck check = await bed.CheckAsync(LayerRequest(bed), Ct);

        check.AssertSays(BlockerKind.Locked, $"{dll} is held open by another program");
        Assert.AreEqual("ReShade64.dll", bed.Bed.LayerFiles());
        Assert.AreEqual("", bed.Bed.User().Snapshot());
    }

    [TestMethod]
    public async Task A_layer_folder_on_a_full_drive_other_than_the_games_is_refused_naming_that_drive()
    {
        using RefusalBed bed = WithPlaces();
        bed.OtherDrives = [(bed.Bed.LayerFolder(), new DriveSpace("L:", 1))];

        InstallCheck check = await bed.CheckAsync(LayerRequest(bed), Ct);

        check.AssertSays(BlockerKind.FreeSpace, "Not enough space on L:");
    }

    // A refusal bed with registries and the layer folder in its data folder.
    private static RefusalBed WithPlaces()
    {
        RefusalBed bed = new(TreeFile.Bytes("Game.exe", GameFolders.Dll("the game")));
        bed.Bed.Shared = new SharedPlaces(new FakeUserRegistry(), new FakeRegistry(), Path.Join(bed.Bed.DataRoot, "reshade-vulkan"));
        return bed;
    }

    // A request for a plan registering the 64-bit Vulkan layer.
    private static RefusalRequest LayerRequest(RefusalBed bed) => bed.Request(bed.Bed.Plan(RouteId.Native, Layer(RegisteredLayer.VulkanLayer64)));
}
