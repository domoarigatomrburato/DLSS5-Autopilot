using Autopilot.Core.Install;
using Autopilot.Core.Tests.TestBeds;
using static Autopilot.Core.Tests.Install.SharedBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>
/// A layer that cannot be registered stops the install and leaves nothing behind: a layer DLL another program holds, a key
/// that refuses writes, or a transaction with no registry or layer folder to write to.
/// </summary>
[TestClass]
public sealed class LayerFailureTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_refresh_is_skipped_when_the_files_already_hold_the_content_even_while_a_program_holds_the_dll()
    {
        using TransactionBed bed = New();
        _ = await bed.CommitAsync(Ct, Layer(RegisteredLayer.VulkanLayer64));
        using TransactionBed other = bed.BesideHolding(bed.LayerFile("ReShade64.dll"));

        _ = await other.CommitAsync(Ct, Layer(RegisteredLayer.VulkanLayer64));

        Assert.AreEqual(bed.Ours(RegisteredLayer.VulkanLayer64), await other.RegistrationsAsync(Ct));
    }

    [TestMethod]
    public async Task A_held_layer_dll_that_differs_stops_the_install_with_what_to_close()
    {
        using TransactionBed bed = New();
        _ = await bed.CommitAsync(Ct, Layer(RegisteredLayer.VulkanLayer64));
        using TransactionBed other = bed.BesideHolding(bed.LayerFile("ReShade64.dll"));
        TreeSnapshot before = other.Snapshot();

        FailureNote failed = (await other.FailAsync(Ct, Dxgi(), NewerLayer())).Note;

        Assert.AreEqual(FailureKind.Locked, failed.Kind);
        Assert.Contains("ReShade64.dll is in use by a running Vulkan or OpenXR program - close it and install again", failed.Text);
        before.AssertSame(other.Snapshot());
        Assert.AreEqual(0, bed.Registered(RegisteredLayer.VulkanLayer64), "the first game still relies on it");
    }

    [TestMethod]
    public async Task A_layer_key_that_refuses_writes_stops_the_install_and_leaves_nothing_behind()
    {
        using TransactionBed bed = New();
        _ = bed.User().RefuseWrites(VulkanKey);
        TreeSnapshot before = bed.Snapshot();

        FailureNote failed = (await bed.FailAsync(Ct, Dxgi(), Layer(RegisteredLayer.VulkanLayer64))).Note;

        Assert.Contains(@"HKCU\Software\Khronos\Vulkan\ImplicitLayers", failed.Text);
        Assert.AreEqual(FailureOutcome.RolledBack, failed.Outcome);
        before.AssertSame(bed.Snapshot());
        Assert.AreEqual("", bed.LayerFiles(), "the layer files written before the registry refused are taken back out");
    }

    [TestMethod]
    public async Task A_layer_key_that_cannot_be_read_stops_the_install_before_anything_is_written()
    {
        using TransactionBed bed = New();
        _ = bed.User().RefuseReads(VulkanKey);

        FailureNote failed = (await bed.FailAsync(Ct, Dxgi(), Layer(RegisteredLayer.VulkanLayer64))).Note;

        Assert.Contains(@"Windows refused to read the registry key HKCU\Software\Khronos\Vulkan\ImplicitLayers", failed.Text);
        Assert.AreEqual(FailureOutcome.NothingWritten, failed.Outcome, "the key is read while the install is planned, before any write");
        Assert.IsFalse(bed.Exists("dxgi.dll"));
    }

    [TestMethod]
    public async Task A_plan_with_a_layer_and_no_registry_is_refused_before_anything_is_written()
    {
        using TransactionBed bed = new();
        TreeSnapshot before = bed.Snapshot();

        InstallRefusal refusal = await bed.RefusalAsync(Ct, Dxgi(), Layer(RegisteredLayer.VulkanLayer64));

        Assert.AreEqual(InstallRefusal.SharedPlacesMissing, refusal);
        before.AssertSame(bed.Snapshot());
    }

    [TestMethod]
    public async Task A_plan_with_a_layer_and_no_layer_folder_is_refused()
    {
        using TransactionBed bed = WithoutLayerFolder();

        InstallRefusal refusal = await bed.RefusalAsync(Ct, Layer(RegisteredLayer.OpenXr));

        Assert.AreEqual(InstallRefusal.SharedPlacesMissing, refusal);
    }

    [TestMethod]
    public async Task An_uninstall_stopped_by_a_refused_registry_delete_names_the_key()
    {
        using TransactionBed bed = New();
        _ = await bed.CommitAsync(Ct, Layer(RegisteredLayer.VulkanLayer64));

        UninstallFailed failed = await bed.UninstallAsAsync<UninstallFailed>(Ct, new RefusingOutside(@"HKCU\Software\Khronos\Vulkan\ImplicitLayers\ReShade64.json"));

        Assert.Contains(@"Windows refused to change the registry value HKCU\Software\Khronos\Vulkan\ImplicitLayers\ReShade64.json", failed.Note.Text);
    }

    private static PlanStep NewerLayer() => Layer(RegisteredLayer.VulkanLayer64, GameFolders.Dll("a newer ReShade64.dll"));
}

/// <summary>Outside changes whose registrations Windows refuses to delete, the refusal escaping to the uninstall.</summary>
internal sealed class RefusingOutside(string place) : IOutsideChanges
{
    public Task<IReadOnlyList<ConfigEdit>> UndoConfigEditsAsync(IReadOnlyList<ConfigEdit> edits, InstallPaths paths, CancellationToken cancellationToken) =>
        Task.FromResult(edits);

    public Task<IReadOnlyList<GraphicsPreferenceEdit>> UndoGraphicsPreferencesAsync(IReadOnlyList<GraphicsPreferenceEdit> preferences, CancellationToken cancellationToken) =>
        Task.FromResult(preferences);

    public Task<IReadOnlyList<LayerRegistration>> UndoRegistrationsAsync(IReadOnlyList<LayerRegistration> registrations, CancellationToken cancellationToken) =>
        throw RegistryChangeException.Write(place, new UnauthorizedAccessException("Access is denied."));
}
