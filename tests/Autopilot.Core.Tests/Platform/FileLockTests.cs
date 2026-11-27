using Autopilot.Core.Platform;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Platform;

/// <summary>An install names a file another program holds before it writes anything, and opening the file to ask changes nothing.</summary>
[TestClass]
public sealed class FileLockTests
{
    [TestMethod]
    public void A_file_held_open_by_another_handle_is_named_as_in_use()
    {
        using var game = GameTree.Of(GameFolders.OwnFiles);
        using FileStream launcher = new(game.PathOf("dxgi.dll"), FileMode.Open, FileAccess.Read, FileShare.None);

        IReadOnlyList<HeldFile> held = FileLocks.Held([game.PathOf("dxgi.dll"), game.PathOf("nvngx_dlss.dll")]);

        CollectionAssert.AreEqual(new[] { new HeldFile(game.PathOf("dxgi.dll"), HeldReason.InUse) }, held.ToArray());
    }

    [TestMethod]
    public void A_writable_file_another_handle_holds_for_reading_is_named_as_in_use()
    {
        using var game = GameTree.Of(GameFolders.OwnFiles);
        using FileStream loader = new(game.PathOf("dxgi.dll"), FileMode.Open, FileAccess.Read, FileShare.Read);

        CollectionAssert.AreEqual(new[] { new HeldFile(game.PathOf("dxgi.dll"), HeldReason.InUse) }, FileLocks.Held([game.PathOf("dxgi.dll")]).ToArray());
    }

    [TestMethod]
    public void Free_files_are_not_held_and_asking_neither_creates_nor_truncates()
    {
        using var game = GameTree.Of(GameFolders.OwnFiles);
        var before = TreeSnapshot.Of(game.Root);

        IReadOnlyList<HeldFile> held = FileLocks.Held([game.PathOf("dxgi.dll"), game.PathOf("d3d11.dll")]);

        Assert.IsEmpty(held);
        before.AssertSame(TreeSnapshot.Of(game.Root));
    }

    [TestMethod]
    public void A_missing_file_is_not_listed()
    {
        using var game = GameTree.Of(GameFolders.OwnFiles);

        Assert.IsEmpty(FileLocks.Held([game.PathOf("absent.dll"), game.PathOf(@"missing\x.dll")]));
        Assert.IsFalse(File.Exists(game.PathOf("absent.dll")));
    }

    [TestMethod]
    public void A_read_only_file_is_not_held()
    {
        using var game = GameTree.Of(new TreeFile("d3dcompiler_47.dll") { Content = GameFolders.Dll(), ReadOnly = true });

        Assert.IsEmpty(FileLocks.Held([game.PathOf("d3dcompiler_47.dll")]));
    }

    [TestMethod]
    public void A_file_the_tool_may_not_open_is_named_as_denied()
    {
        using var game = GameTree.Of(GameFolders.OwnFiles);
        if (OperatingSystem.IsWindows() || Environment.IsPrivilegedProcess)
        {
            Assert.Inconclusive("taking read access away is a Unix file mode here, and a privileged process keeps it");
            return;
        }

        File.SetUnixFileMode(game.PathOf("dxgi.dll"), UnixFileMode.None);

        CollectionAssert.AreEqual(new[] { new HeldFile(game.PathOf("dxgi.dll"), HeldReason.Denied) }, FileLocks.Held([game.PathOf("dxgi.dll")]).ToArray());
    }
}
