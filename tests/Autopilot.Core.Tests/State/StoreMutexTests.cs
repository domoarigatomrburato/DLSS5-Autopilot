using Autopilot.Core.State;

namespace Autopilot.Core.Tests.State;

/// <summary>A game's mutex that cannot be opened (an elevated CLI made it, and it refuses the app) costs nothing the tool learned and throws nothing.</summary>
[TestClass]
public sealed class StoreMutexTests(TestContext testContext)
{
    private CancellationToken Cancellation => testContext.CancellationToken;

    [TestMethod]
    public void A_change_is_kept_in_memory_said_once_and_written_once_the_mutex_opens()
    {
        using StateBed bed = new();
        bed.MutexRefused = true;

        bed.Set("attempts", 1, Cancellation);
        bed.Set("fixes", 1, Cancellation);

        Assert.IsFalse(File.Exists(bed.File));
        Assert.AreEqual(1, bed.Get("attempts"));
        CollectionAssert.AreEqual(new[] { StoreNoticeKind.NotSaved }, bed.Notices());

        bed.MutexRefused = false;
        bed.Set("cli", 1, Cancellation);

        Assert.AreEqual(1, (int)bed.OnDisk()["attempts"]!);
        Assert.AreEqual(1, (int)bed.OnDisk()["fixes"]!);
    }

    [TestMethod]
    public void A_drop_is_kept_until_a_write_can_carry_it()
    {
        using StateBed bed = new();
        bed.Set("attempts", 1, Cancellation);
        bed.MutexRefused = true;

        bed.Store.Drop(bed.Game, Cancellation);

        Assert.IsTrue(File.Exists(bed.File));
        Assert.AreEqual(0, bed.Get("attempts"));
        CollectionAssert.AreEqual(new[] { StoreNoticeKind.NotSaved }, bed.Notices());

        bed.MutexRefused = false;
        bed.Set("cli", 1, Cancellation);

        Assert.IsFalse(bed.OnDisk().ContainsKey("attempts"));
        Assert.AreEqual(1, (int)bed.OnDisk()["cli"]!);
        CollectionAssert.AreEqual(new[] { bed.Game.Folder }, bed.Store.Folders().ToArray(), "listed again once it holds state");
    }

    [TestMethod]
    public void A_damaged_file_is_left_in_place_while_its_mutex_cannot_be_opened()
    {
        using StateBed bed = new();
        bed.Plant("{not json");
        bed.MutexRefused = true;

        Assert.AreEqual(0, bed.Get("attempts"));

        Assert.AreEqual("{not json", File.ReadAllText(bed.File));
        Assert.IsFalse(File.Exists(bed.File + ".corrupt"));
    }
}
