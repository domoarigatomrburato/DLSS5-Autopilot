using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Tests.TestBeds;
using static Autopilot.Core.Tests.Install.SharedBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>
/// A shared layer goes with the last install that relies on it, as the install records say: never while another
/// install's record lists it or cannot be read, and never one we did not create.
/// </summary>
[TestClass]
public sealed class DependentsTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task Two_games_share_one_layer_and_the_last_uninstall_removes_it_with_its_files()
    {
        using TransactionBed first = New();
        using TransactionBed second = first.Beside(GameFolders.Empty);
        await InstallAsync(first);
        await InstallAsync(second);

        _ = await first.UninstallFinishedAsync(Ct);
        object? afterFirst = first.Registered(RegisteredLayer.VulkanLayer64);
        string filesAfterFirst = first.LayerFiles();
        _ = await second.UninstallFinishedAsync(Ct);

        Assert.AreEqual(0, afterFirst, "the second game still relies on it");
        Assert.AreEqual("ReShade64.dll|ReShade64.json", filesAfterFirst);
        Assert.IsNull(first.Registered(RegisteredLayer.VulkanLayer64));
        Assert.AreEqual("", first.LayerFiles());
    }

    [TestMethod]
    public async Task A_dependent_whose_record_cannot_be_read_keeps_the_layer_and_the_log_says_why()
    {
        using TransactionBed first = New();
        using TransactionBed second = first.Beside(GameFolders.Empty);
        await InstallAsync(first);
        await InstallAsync(second);
        TestFiles.Plant(second.Tree.PathOf(KnownFiles.InstallRecord), "{ damaged");

        _ = await first.UninstallFinishedAsync(Ct);

        Assert.AreEqual(0, first.Registered(RegisteredLayer.VulkanLayer64));
        Assert.IsTrue(first.Says("its install record cannot be read"));
    }

    [TestMethod]
    public async Task A_game_whose_record_is_gone_is_no_dependent()
    {
        using TransactionBed first = New();
        using TransactionBed second = first.Beside(GameFolders.Empty);
        await InstallAsync(first);
        await InstallAsync(second);
        File.Delete(second.Tree.PathOf(KnownFiles.InstallRecord));

        _ = await first.UninstallFinishedAsync(Ct);

        Assert.IsNull(first.Registered(RegisteredLayer.VulkanLayer64));
        Assert.AreEqual("", first.LayerFiles());
    }

    [TestMethod]
    public async Task A_game_installed_through_a_link_is_one_folder_and_not_its_own_dependent()
    {
        using TransactionBed bed = New();
        await InstallThroughLinkAsync(bed);
        await InstallAsync(bed);
        int folders = bed.Store.Folders().Count;

        _ = await bed.UninstallFinishedAsync(Ct);

        Assert.AreEqual(1, folders);
        Assert.IsNull(bed.Registered(RegisteredLayer.VulkanLayer64));
    }

    [TestMethod]
    public async Task A_reused_foreign_layer_stays_even_after_its_last_dependent()
    {
        using TransactionBed bed = New();
        string foreign = Foreign(bed);
        TestFiles.Plant(foreign, /*lang=json,strict*/ """{ "layer": { "library_path": "ReShade64.dll" } }""");
        _ = bed.User().Set(VulkanKey, foreign, 0);
        await InstallAsync(bed);

        _ = await bed.UninstallFinishedAsync(Ct);

        Assert.AreEqual(0, bed.User().Value(VulkanKey, foreign));
        Assert.IsTrue(File.Exists(foreign));
    }

    [TestMethod]
    public async Task A_record_naming_a_foreign_manifest_as_ours_never_removes_it()
    {
        using TransactionBed bed = New();
        string foreign = PlantForgedRecord(bed);

        _ = await bed.UninstallFinishedAsync(Ct);

        Assert.AreEqual(0, bed.User().Value(VulkanKey, foreign));
        Assert.IsTrue(bed.Says("is not our layer"));
    }

    // A foreign layer, registered, and a record that names its manifest as ours.
    private static string PlantForgedRecord(TransactionBed bed)
    {
        string foreign = Foreign(bed);
        _ = bed.User().Set(VulkanKey, foreign, 0);
        Recorded.Plant(bed.Tree.Root, Recorded.Install() with { Registrations = [new(RegisteredLayer.VulkanLayer64, false, foreign)] });
        return foreign;
    }

    private static string Foreign(TransactionBed bed) => Path.GetFullPath(Path.Join(bed.Tree.Root, "..", "ReShade", "ReShade64.json"));

    // The bed with the 64-bit Vulkan layer installed through a link to its game folder.
    private async Task InstallThroughLinkAsync(TransactionBed bed)
    {
        string link = bed.Tree.Root + "-link";
        _ = Directory.CreateSymbolicLink(link, bed.Tree.Root);
        InstallPlan plan = bed.Plan(Bitness.Bits64, Layer(RegisteredLayer.VulkanLayer64));
        _ = await bed.CommitAsync(plan with { GameFolder = link, InstallFolder = link }, Ct);
    }

    // The bed with the 64-bit Vulkan layer installed.
    private async Task InstallAsync(TransactionBed bed) => _ = await bed.CommitAsync(Ct, Layer(RegisteredLayer.VulkanLayer64));
}
