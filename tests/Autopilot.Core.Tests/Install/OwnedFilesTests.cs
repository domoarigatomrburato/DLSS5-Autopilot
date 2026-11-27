using Autopilot.Core.Install;
using Autopilot.Core.Tests.Net;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>
/// Which files are ours, by the record and the hash: a file a game update replaced is the game's again,
/// a config our component rewrote stays ours, and a backup counts only while it holds the recorded original.
/// </summary>
[TestClass]
public sealed class OwnedFilesTests(TestContext testContext)
{
    private static readonly byte[] _ours = GameFolders.Dll("ours");

    private static readonly byte[] _games = GameFolders.Dll("the game's");

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_file_still_as_we_wrote_it_is_ours()
    {
        using RecordBed bed = new(TreeFile.Bytes("dxgi.dll", _ours));
        OwnedFiles owned = await OwnedAsync(bed, Recorded.Ours("dxgi.dll", Facts(_ours)));

        Assert.IsTrue(await owned.IsOursAsync(bed.Tree.PathOf("dxgi.dll"), Ct));
        Assert.IsTrue(owned.IsListedAsOurs(bed.Tree.PathOf("DXGI.DLL")));
    }

    [TestMethod]
    public async Task Our_dxgi_a_game_update_replaced_is_not_ours()
    {
        using RecordBed bed = new(TreeFile.Bytes("dxgi.dll", _games));
        OwnedFiles owned = await OwnedAsync(bed, Recorded.Ours("dxgi.dll", Facts(_ours)));

        Assert.IsFalse(await owned.IsOursAsync(bed.Tree.PathOf("dxgi.dll"), Ct));
    }

    [TestMethod]
    public async Task A_file_of_our_size_with_other_bytes_is_not_ours()
    {
        byte[] same = [.. _ours];
        same[^1] ^= 0xFF;
        using RecordBed bed = new(TreeFile.Bytes("dxgi.dll", same));
        OwnedFiles owned = await OwnedAsync(bed, Recorded.Ours("dxgi.dll", Facts(_ours)));

        Assert.IsFalse(await owned.IsOursAsync(bed.Tree.PathOf("dxgi.dll"), Ct));
    }

    [TestMethod]
    public async Task A_config_file_its_component_rewrote_is_ours()
    {
        using RecordBed bed = new(TreeFile.Text("ReShade.ini", "[GENERAL]\r\nPresetPath=.\\changed-while-running.ini\r\n"));
        OwnedFiles owned = await OwnedAsync(bed, Recorded.Ours("ReShade.ini", Facts("[GENERAL]\r\n"u8.ToArray())) with { Class = EntryClass.Config });

        Assert.IsTrue(await owned.IsOursAsync(bed.Tree.PathOf("ReShade.ini"), Ct));
    }

    [TestMethod]
    public async Task A_moved_aside_entry_is_not_ours()
    {
        using RecordBed bed = new(TreeFile.Bytes("d3dcompiler_47.dll", _games), TreeFile.Bytes("d3dcompiler_47.dll.dlss5-off", _games));
        RecordEntry aside = new("d3dcompiler_47.dll", EntryClass.Component, EntryState.MovedAside, Facts(_games), Original(_games, BackupKind.Dlss5Off));
        OwnedFiles owned = await OwnedAsync(bed, aside);

        Assert.IsFalse(await owned.IsOursAsync(bed.Tree.PathOf("d3dcompiler_47.dll"), Ct));
        Assert.IsFalse(owned.IsListedAsOurs(bed.Tree.PathOf("d3dcompiler_47.dll")));
        Assert.IsFalse(owned.IsListed(bed.Tree.PathOf("d3dcompiler_47.dll")));
    }

    [TestMethod]
    public async Task A_missing_file_is_not_ours()
    {
        using RecordBed bed = new();
        OwnedFiles owned = await OwnedAsync(bed, Recorded.Ours("dxgi.dll", Facts(_ours)));

        Assert.IsFalse(await owned.IsOursAsync(bed.Tree.PathOf("dxgi.dll"), Ct));
    }

    [TestMethod]
    public async Task A_swapped_file_is_ours_by_its_hash_but_listed_as_the_games()
    {
        using RecordBed bed = new(TreeFile.Bytes("nvngx_dlss.dll", _ours), TreeFile.Bytes("nvngx_dlss.dll.invidia-claritatis-backup", _games));
        OwnedFiles owned = await OwnedAsync(bed, Recorded.Swapped("nvngx_dlss.dll", Facts(_games), Facts(_ours)));

        Assert.IsTrue(await owned.IsOursAsync(bed.Tree.PathOf("nvngx_dlss.dll"), Ct), "install writes over it and keeps the backup");
        Assert.IsFalse(owned.IsListedAsOurs(bed.Tree.PathOf("nvngx_dlss.dll")), "the game ships it: evidence of the game");
        Assert.IsTrue(owned.IsListed(bed.Tree.PathOf("nvngx_dlss.dll")));
    }

    [TestMethod]
    [DataRow(BackupKind.Backup, ".invidia-claritatis-backup")]
    [DataRow(BackupKind.Dlss5Off, ".dlss5-off")]
    public async Task A_backup_holding_the_recorded_original_is_valid(BackupKind kind, string suffix)
    {
        using RecordBed bed = new(TreeFile.Bytes("bin/d3dcompiler_47.dll" + suffix, _games));
        RecordEntry entry = Recorded.Ours("bin/d3dcompiler_47.dll") with { Original = Original(_games, kind) };
        OwnedFiles owned = await OwnedAsync(bed, entry);

        Assert.IsTrue(await owned.IsBackupValidAsync(entry, Ct));
    }

    [TestMethod]
    public async Task A_backup_whose_bytes_differ_from_the_recorded_original_is_invalid()
    {
        using RecordBed bed = new(TreeFile.Bytes("dxgi.dll.invidia-claritatis-backup", _ours));
        RecordEntry entry = Recorded.Swapped("dxgi.dll", Facts(_games));
        OwnedFiles owned = await OwnedAsync(bed, entry);

        Assert.IsFalse(await owned.IsBackupValidAsync(entry, Ct));
    }

    [TestMethod]
    public async Task A_missing_backup_is_invalid()
    {
        using RecordBed bed = new(TreeFile.Bytes("dxgi.dll", _ours));
        RecordEntry entry = Recorded.Swapped("dxgi.dll", Facts(_games));
        OwnedFiles owned = await OwnedAsync(bed, entry);

        Assert.IsFalse(await owned.IsBackupValidAsync(entry, Ct));
        Assert.IsFalse(await owned.IsBackupValidAsync(Recorded.Ours("dxgi.dll"), Ct), "no original, no backup");
    }

    [TestMethod]
    public async Task Without_a_record_or_with_an_unreadable_one_nothing_is_ours()
    {
        using RecordBed bed = new(TreeFile.Bytes("dxgi.dll", _ours), TreeFile.Bytes("dxgi.dll.invidia-claritatis-backup", _games));
        RecordEntry entry = Recorded.Swapped("dxgi.dll", Facts(_games), Facts(_ours));
        bed.PlantRaw("{ damaged");
        RecordedInstall unreadable = (await bed.FindAsync("", Ct))!;

        foreach (OwnedFiles owned in new[] { new OwnedFiles(null), new OwnedFiles(unreadable) })
        {
            Assert.IsFalse(owned.IsListedAsOurs(bed.Tree.PathOf("dxgi.dll")));
            Assert.IsFalse(await owned.IsOursAsync(bed.Tree.PathOf("dxgi.dll"), Ct));
            Assert.IsFalse(await owned.IsBackupValidAsync(entry, Ct));
        }
    }

    [TestMethod]
    public async Task A_path_outside_the_game_folder_is_never_ours()
    {
        using RecordBed bed = new(TreeFile.Bytes("dxgi.dll", _ours));
        OwnedFiles owned = await OwnedAsync(bed, Recorded.Ours("dxgi.dll", Facts(_ours)));

        Assert.IsFalse(owned.IsListedAsOurs(Path.Join(Path.GetDirectoryName(bed.Tree.Root), "dxgi.dll")));
        Assert.IsFalse(owned.IsListedAsOurs(bed.Tree.Root));
    }

    [TestMethod]
    public async Task In_an_unreal_layout_paths_are_relative_to_the_game_folder_not_the_install_folder()
    {
        using RecordBed bed = new([.. GameFolders.Unreal, TreeFile.Bytes("Binaries/Win64/dxgi.dll", _ours), TreeFile.Bytes("Plugins/DLSS/Binaries/ThirdParty/Win64/nvngx_dlss.dll.invidia-claritatis-backup", _games)]);
        string install = bed.Tree.PathOf("Binaries/Win64");
        RecordEntry runtime = Recorded.Swapped("Plugins/DLSS/Binaries/ThirdParty/Win64/nvngx_dlss.dll", Facts(_games));
        Recorded.Plant(install, Recorded.Install("Binaries/Win64", Recorded.Ours("Binaries/Win64/dxgi.dll", Facts(_ours)), runtime));
        OwnedFiles owned = new(await bed.Records.FindAsync(install, Ct));

        Assert.IsTrue(await owned.IsOursAsync(bed.Tree.PathOf("Binaries/Win64/dxgi.dll"), Ct));
        Assert.IsFalse(owned.IsListedAsOurs(bed.Tree.PathOf("dxgi.dll")), "the same name in the game folder's root is not on the record");
        Assert.IsTrue(await owned.IsBackupValidAsync(runtime, Ct));
    }

    private static FileFacts Facts(byte[] content) => new(CacheFixtures.DigestOf(content), content.Length);

    private static OriginalFile Original(byte[] content, BackupKind kind) => new(Facts(content), FileAttributes.Normal, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), kind);

    // The record in the game folder's root, read back through the reader.
    private async Task<OwnedFiles> OwnedAsync(RecordBed bed, params RecordEntry[] entries)
    {
        Recorded.Plant(bed.Tree.Root, Recorded.Install("", entries));
        return new OwnedFiles(await bed.FindAsync("", Ct));
    }
}
