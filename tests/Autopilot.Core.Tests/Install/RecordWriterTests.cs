using Autopilot.Core.Install;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>The record as install writes it: whole or not at all, in format 1, and read back as written.</summary>
[TestClass]
public sealed class RecordWriterTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_saved_record_reads_back_as_it_was_written()
    {
        using RecordBed bed = new(GameFolders.Unreal);

        await bed.SaveAsync(FullRecord.Value, Ct, "Binaries/Win64");
        RecordedInstall found = await bed.FoundAsync("Binaries/Win64", Ct);

        Assert.AreEqual(InstallState.Installing, found.State);
        FullRecord.AssertSame(FullRecord.Value, found.Record!);
        Assert.IsEmpty(bed.LogLines);
    }

    [TestMethod]
    public async Task Save_always_writes_format_1()
    {
        using RecordBed bed = new();

        await bed.SaveAsync(Recorded.Install() with { FormatVersion = 7 }, Ct);

        Assert.AreEqual(1, (int?)bed.OnDisk()["version"]);
        Assert.AreEqual(InstallState.Installed, (await bed.FoundAsync("", Ct)).State);
    }

    [TestMethod]
    public async Task Save_replaces_the_record_whole_and_leaves_no_temporary_file()
    {
        using RecordBed bed = new(GameFolders.OwnFiles);
        await bed.SaveAsync(Recorded.Install("", Recorded.Ours("dxgi.dll")), Ct);

        await bed.SaveAsync(Recorded.Install() with { State = RecordState.Removing }, Ct);

        RecordedInstall found = await bed.FoundAsync("", Ct);
        Assert.AreEqual(InstallState.Removing, found.State);
        Assert.IsEmpty(found.Record!.Entries);
        Assert.AreEqual("ReShade.ini, d3dcompiler_47.dll, dxgi.dll, invidia-claritatis.json, nvngx_dlss.dll", bed.Files);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    public async Task A_save_that_fails_leaves_the_old_record_whole()
    {
        using RecordBed bed = new();
        await bed.SaveAsync(Recorded.Install("", Recorded.Ours("dxgi.dll")), Ct);
        string before = await File.ReadAllTextAsync(bed.RecordPath(), Ct);
        // A folder that takes no new file, as an unwritable game folder: the temporary file cannot be made.
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(bed.Tree.Root, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }

        if (CanCreateIn(bed.Tree.Root))
        {
            Assert.Inconclusive("running as root, where folder permissions do not bind");
        }

        _ = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => bed.SaveAsync(Recorded.Install(), Ct));

        Assert.AreEqual(before, await File.ReadAllTextAsync(bed.RecordPath(), Ct));
        Assert.AreEqual("invidia-claritatis.json", bed.Files);
    }

    [TestMethod]
    public async Task An_original_captured_with_a_cloud_files_attribute_saves_and_reads_back_with_the_attributes_a_restore_sets()
    {
        using RecordBed bed = new();
        RecordEntry swapped = Recorded.Swapped("dxgi.dll");
        // 0x400000 is Windows' recall-on-data-access bit, which OneDrive sets and .NET has no name for.
        RecordEntry cloud = swapped with { Original = swapped.Original! with { Attributes = FileAttributes.ReadOnly | FileAttributes.Normal | (FileAttributes)0x400000 } };

        await bed.SaveAsync(Recorded.Install("", cloud), Ct);

        Assert.AreEqual(FileAttributes.ReadOnly, (await bed.FoundAsync("", Ct)).Record!.Entries.Single().Original!.Attributes);
    }

    [TestMethod]
    public async Task Delete_removes_the_record_and_a_missing_one_is_not_an_error()
    {
        using RecordBed bed = new(GameFolders.OwnFiles);
        await bed.SaveAsync(Recorded.Install(), Ct);

        await InstallRecords.DeleteAsync(bed.Tree.Root, Ct);
        await InstallRecords.DeleteAsync(bed.Tree.Root, Ct);
        await InstallRecords.DeleteAsync(bed.Tree.PathOf("No Such Folder"), Ct);

        Assert.IsNull(await bed.FindAsync("", Ct));
        Assert.AreEqual("ReShade.ini, d3dcompiler_47.dll, dxgi.dll, nvngx_dlss.dll", bed.Files);
    }

    [TestMethod]
    public async Task A_read_only_record_is_replaced_by_a_save()
    {
        using RecordBed bed = new();
        await bed.SaveAsync(Recorded.Install(), Ct);
        File.SetAttributes(bed.RecordPath(), FileAttributes.ReadOnly);

        await bed.SaveAsync(Recorded.Install() with { State = RecordState.Removing }, Ct);

        Assert.AreEqual(InstallState.Removing, (await bed.FoundAsync("", Ct)).State);
        Assert.IsFalse(File.GetAttributes(bed.RecordPath()).HasFlag(FileAttributes.ReadOnly));
    }

    [TestMethod]
    public async Task A_read_only_record_is_removed_by_a_delete()
    {
        using RecordBed bed = new();
        await bed.SaveAsync(Recorded.Install(), Ct);
        File.SetAttributes(bed.RecordPath(), FileAttributes.ReadOnly);

        await InstallRecords.DeleteAsync(bed.Tree.Root, Ct);

        Assert.AreEqual("", bed.Files);
    }

    [TestMethod]
    public async Task A_cancelled_save_writes_nothing()
    {
        using RecordBed bed = new();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => bed.SaveAsync(Recorded.Install(), new CancellationToken(canceled: true)));

        Assert.AreEqual("", bed.Files);
    }

    [TestMethod]
    public async Task A_cancelled_delete_removes_nothing()
    {
        using RecordBed bed = new();
        Recorded.Plant(bed.Tree.Root, Recorded.Install());

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => InstallRecords.DeleteAsync(bed.Tree.Root, new CancellationToken(canceled: true)));

        Assert.AreEqual("invidia-claritatis.json", bed.Files);
    }

    private static bool CanCreateIn(string folder)
    {
        try
        {
            using (File.Create(Path.Join(folder, "probe")))
            {
            }

            File.Delete(Path.Join(folder, "probe"));
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
