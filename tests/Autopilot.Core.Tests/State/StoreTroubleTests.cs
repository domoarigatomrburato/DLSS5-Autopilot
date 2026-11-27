using System.Text.Json.Nodes;
using Autopilot.Core.State;

namespace Autopilot.Core.Tests.State;

/// <summary>Unreadable is not empty: a newer, locked or damaged file and a failed write never cost what the tool learned.</summary>
[TestClass]
public sealed class StoreTroubleTests(TestContext testContext)
{
    private CancellationToken Cancellation => testContext.CancellationToken;

    [TestMethod]
    public void A_newer_versions_file_is_read_where_it_can_be_and_never_written()
    {
        using StateBed bed = new();
        const string Newer = /*lang=json,strict*/ """{"schema": 2, "folder": "x", "attempts": 4, "shape": {"v2": true}}""";
        bed.Plant(Newer);

        bed.Set("attempts", 5, Cancellation);
        bed.Set("fixes", 1, Cancellation);
        bed.Store.Drop(bed.Game, Cancellation);

        Assert.AreEqual(Newer, File.ReadAllText(bed.File), "untouched, even by a drop");
        Assert.AreEqual(4, bed.NewStore().Read(bed.Game).Get("attempts", StateJson.Default.Int32));
        CollectionAssert.AreEqual(new[] { StoreNoticeKind.NewerVersion }, bed.Notices(), "said once");
    }

    [TestMethod]
    public void A_change_to_a_newer_versions_file_is_kept_in_memory_for_the_process()
    {
        using StateBed bed = new();
        bed.Plant(/*lang=json,strict*/ """{"schema": 2, "attempts": 4}""");

        bed.Set("attempts", 5, Cancellation);

        Assert.AreEqual(5, bed.Get("attempts"));
        Assert.AreEqual(2, bed.Store.Read(bed.Game).Get("schema", StateJson.Default.Int32), "read as the newer version wrote it");
    }

    [TestMethod]
    public void A_file_locked_for_a_while_is_retried_then_the_last_good_copy_is_used_and_nothing_is_written()
    {
        using StateBed bed = new();
        bed.Set("attempts", 1, Cancellation);
        string written = File.ReadAllText(bed.File);
        _ = bed.Notices();

        using (FileStream antivirus = new(bed.File, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            bed.Set("fixes", 2, Cancellation);

            Assert.AreEqual(1, bed.Get("attempts"), "the last good copy");
            Assert.AreEqual(2, bed.Get("fixes"), "the change, kept in memory");
        }

        Assert.AreEqual(written, File.ReadAllText(bed.File), "not written from a read that failed");
        CollectionAssert.AreEqual(new[] { StoreNoticeKind.Unreadable }, bed.Notices(), "said once");
        Assert.IsTrue(bed.Time.Waits.All(wait => wait == TimeSpan.FromMilliseconds(100)));
        Assert.HasCount(6, bed.Time.Waits, "three tries for the write and for each of the two reads");
    }

    [TestMethod]
    public void A_change_kept_while_the_file_was_locked_is_written_with_the_next_one_onto_what_the_file_holds_then()
    {
        using StateBed bed = new();
        bed.Set("attempts", 1, Cancellation);
        using (new FileStream(bed.File, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            bed.Set("fixes", 2, Cancellation);
        }

        bed.NewStore().Update(bed.Game, state => state.Set("cli", 7, StateJson.Default.Int32), Cancellation);
        bed.Set("attempts", 3, Cancellation);

        JsonObject saved = bed.OnDisk();
        Assert.AreEqual(3, (int)saved["attempts"]!);
        Assert.AreEqual(2, (int)saved["fixes"]!);
        Assert.AreEqual(7, (int)saved["cli"]!, "what another process wrote meanwhile is kept");
    }

    [TestMethod]
    [DataRow("", DisplayName = "an empty file")]
    [DataRow("""{"schema": 1, "attempts": """, DisplayName = "truncated JSON")]
    [DataRow("[1, 2]", DisplayName = "a JSON array")]
    [DataRow(/*lang=json*/ """{"a": 1, "a": 2}""", DisplayName = "a key twice")]
    public void A_damaged_file_is_moved_aside_and_the_store_starts_from_defaults(string damaged)
    {
        using StateBed bed = new();
        bed.Plant(damaged);
        TestFiles.Plant(bed.File + ".corrupt", "an older damaged copy");

        int attempts = bed.Get("attempts");

        Assert.AreEqual(0, attempts);
        Assert.AreEqual(damaged, File.ReadAllText(bed.File + ".corrupt"), "the newest damaged copy is kept");
        Assert.IsFalse(File.Exists(bed.File));
        StoreNotice notice = bed.Store.TakeNotices().Single();
        Assert.AreEqual(StoreNoticeKind.Damaged, notice.Kind);
        Assert.AreEqual(bed.Game.Folder, notice.Folder);
        Assert.AreEqual(
            $"What the tool learned about {bed.Game.Folder} could not be read, so it starts afresh for this game. The damaged file is kept as {bed.Game.Key}.json.corrupt in the tool's games folder.",
            notice.Text);
    }

    [TestMethod]
    public void A_file_too_large_to_be_the_stores_own_is_damaged()
    {
        using StateBed bed = new();
        bed.Plant(/*lang=json,strict*/ $$"""{"schema": 1, "pad": "{{new string('x', 1024 * 1024)}}"}""");

        bed.Set("attempts", 1, Cancellation);

        Assert.AreEqual(1, (int)bed.OnDisk()["attempts"]!);
        Assert.IsTrue(File.Exists(bed.File + ".corrupt"));
        Assert.IsEmpty(bed.Store.Folders().Where(folder => folder != bed.Game.Folder));
    }

    [TestMethod]
    public void A_damaged_file_found_by_a_write_is_moved_aside_and_the_change_written_fresh()
    {
        using StateBed bed = new();
        bed.Plant("{not json");

        bed.Set("attempts", 1, Cancellation);
        bed.Set("fixes", 1, Cancellation);

        Assert.AreEqual(1, (int)bed.OnDisk()["attempts"]!);
        Assert.AreEqual("{not json", File.ReadAllText(bed.File + ".corrupt"));
        CollectionAssert.AreEqual(new[] { StoreNoticeKind.Damaged }, bed.Notices());
    }

    [TestMethod]
    public void A_damaged_file_that_cannot_be_moved_aside_is_never_written_over()
    {
        using StateBed bed = new();
        bed.Plant("{not json");
        _ = Directory.CreateDirectory(bed.File + ".corrupt");

        bed.Set("attempts", 1, Cancellation);

        Assert.AreEqual("{not json", File.ReadAllText(bed.File));
        Assert.AreEqual(1, bed.Get("attempts"), "kept in memory");
        CollectionAssert.AreEquivalent(new[] { StoreNoticeKind.Damaged, StoreNoticeKind.NotSaved }, bed.Notices());
    }

    [TestMethod]
    public void A_full_drive_keeps_the_value_in_memory_says_so_once_and_saves_it_once_there_is_room()
    {
        using StateBed bed = new();
        bed.DriveFull = true;

        bed.Set("attempts", 1, Cancellation);
        bed.Set("attempts", 2, Cancellation);

        Assert.IsFalse(File.Exists(bed.File));
        Assert.AreEqual(2, bed.Get("attempts"));
        CollectionAssert.AreEqual(new[] { bed.Game.Folder }, bed.Store.Folders().ToArray(), "a game kept in memory is known");
        Assert.AreEqual(
            $"The drive is full, so what the tool learned about {bed.Game.Folder} could not be saved. It is kept until the tool closes; free some space to keep it.",
            bed.Store.TakeNotices().Single().Text);
        Assert.IsEmpty(Directory.GetFiles(Path.GetDirectoryName(bed.File)!), "no temporary file left");

        bed.DriveFull = false;
        bed.Set("fixes", 1, Cancellation);

        Assert.AreEqual(2, (int)bed.OnDisk()["attempts"]!);
        Assert.AreEqual(1, (int)bed.OnDisk()["fixes"]!);
    }

    [TestMethod]
    public void A_removal_the_full_drive_stopped_stays_removed_and_is_saved_once_there_is_room()
    {
        using StateBed bed = new();
        bed.Set("attempts", 1, Cancellation);
        bed.DriveFull = true;

        bed.Store.Update(bed.Game, state => state.Remove("attempts"), Cancellation);
        bed.DriveFull = false;
        bed.NewStore().Update(bed.Game, state => state.Set("cli", 7, StateJson.Default.Int32), Cancellation);

        Assert.AreEqual(1, (int)bed.OnDisk()["attempts"]!, "the other process wrote back what it read");
        Assert.AreEqual(0, bed.Get("attempts"), "still removed for this process");
        Assert.AreEqual(7, bed.Get("cli"), "what another process wrote meanwhile reads through");

        bed.Set("fixes", 1, Cancellation);

        JsonObject saved = bed.OnDisk();
        Assert.IsFalse(saved.ContainsKey("attempts"));
        Assert.AreEqual(7, (int)saved["cli"]!);
        Assert.AreEqual(1, (int)saved["fixes"]!);
    }

    [TestMethod]
    public void Only_the_keys_a_kept_change_touched_are_laid_over_what_another_process_writes()
    {
        using StateBed bed = new();
        bed.Set("attempts", 1, Cancellation);
        bed.DriveFull = true;

        bed.Set("fixes", 1, Cancellation);
        bed.DriveFull = false;
        bed.NewStore().Update(bed.Game, state => state.Set("attempts", 5, StateJson.Default.Int32), Cancellation);

        Assert.AreEqual(5, bed.Get("attempts"), "the value this process read before is not laid over");
        Assert.AreEqual(1, bed.Get("fixes"));

        bed.Set("cli", 1, Cancellation);

        Assert.AreEqual(5, (int)bed.OnDisk()["attempts"]!);
        Assert.AreEqual(1, (int)bed.OnDisk()["fixes"]!);
    }

    [TestMethod]
    public void A_folder_that_cannot_be_written_keeps_the_value_in_memory_and_says_so_once()
    {
        using StateBed bed = new();
        TestFiles.Plant(Path.Join(bed.Root, "games"), "a file where the folder should be");

        bed.Set("attempts", 1, Cancellation);
        bed.Set("attempts", 2, Cancellation);

        Assert.AreEqual(2, bed.Get("attempts"));
        StoreNotice notice = bed.Store.TakeNotices().Single();
        Assert.AreEqual(StoreNoticeKind.NotSaved, notice.Kind);
        Assert.AreEqual($"What the tool learned about {bed.Game.Folder} could not be saved: its file cannot be written. It is kept until the tool closes.", notice.Text);
        Assert.IsEmpty(bed.Store.TakeNotices(), "taken");
    }

    [TestMethod]
    public void Dropping_a_damaged_file_keeps_it_aside()
    {
        using StateBed bed = new();
        bed.Plant("{not json");

        bed.Store.Drop(bed.Game, Cancellation);

        Assert.IsFalse(File.Exists(bed.File));
        Assert.AreEqual("{not json", File.ReadAllText(bed.File + ".corrupt"));
    }

    [TestMethod]
    public void A_drop_that_cannot_delete_the_file_reads_as_dropped_until_one_can()
    {
        using StateBed bed = new();
        bed.Set("attempts", 1, Cancellation);
        if (OperatingSystem.IsWindows() || Environment.IsPrivilegedProcess)
        {
            Assert.Inconclusive("taking delete access away is a Unix file mode here, and a privileged process keeps it");
            return;
        }

        string games = Path.GetDirectoryName(bed.File)!;
        File.SetUnixFileMode(games, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            bed.Store.Drop(bed.Game, Cancellation);

            Assert.IsTrue(File.Exists(bed.File));
            Assert.AreEqual(0, bed.Get("attempts"), "dropped for this process");
            Assert.IsEmpty(bed.Store.Folders(), "a dropped game is not listed");
            Assert.AreEqual(1, bed.NewStore().Read(bed.Game).Get("attempts", StateJson.Default.Int32), "the file is still there for another process");
            CollectionAssert.AreEqual(new[] { StoreNoticeKind.NotSaved }, bed.Notices());
        }
        finally
        {
            File.SetUnixFileMode(games, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        bed.Store.Drop(bed.Game, Cancellation);

        Assert.IsFalse(File.Exists(bed.File));
        Assert.AreEqual(0, bed.Get("attempts"));
        Assert.IsEmpty(bed.Store.Folders());
    }

    [TestMethod]
    public void A_folder_where_the_file_should_be_is_damaged_never_moved_or_written_over()
    {
        using StateBed bed = new();
        _ = Directory.CreateDirectory(bed.File);

        bed.Set("attempts", 1, Cancellation);
        bed.Store.Drop(bed.Game, Cancellation);
        bed.Set("fixes", 1, Cancellation);

        Assert.IsTrue(Directory.Exists(bed.File));
        Assert.IsFalse(Path.Exists(bed.File + ".corrupt"));
        Assert.AreEqual(1, bed.Get("fixes"), "kept in memory");
        CollectionAssert.AreEquivalent(new[] { StoreNoticeKind.Damaged, StoreNoticeKind.NotSaved }, bed.Notices());
        Assert.IsEmpty(bed.Time.Waits, "not retried as a locked file");
    }
}
