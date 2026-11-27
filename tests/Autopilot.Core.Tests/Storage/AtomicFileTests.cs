using Autopilot.Core.Storage;

namespace Autopilot.Core.Tests.Storage;

/// <summary>A file replaced whole or not at all.</summary>
[TestClass]
public sealed class AtomicFileTests(TestContext testContext) : AtomicFileTestBase(testContext)
{
    [TestMethod]
    public async Task A_write_creates_the_folder_and_replaces_the_file_whole()
    {
        await WriteAsync("first, and longer");
        await WriteAsync("second");

        Assert.AreEqual("second", await File.ReadAllTextAsync(Target, Ct));
        Assert.HasCount(1, FilesBesideTarget());
    }

    [TestMethod]
    public async Task A_write_that_fails_half_way_leaves_the_old_file_and_no_temporary_file()
    {
        await WriteAsync("old");

        _ = await Assert.ThrowsAsync<IOException>(() => AtomicFile.WriteAsync(Target, async (file, token) =>
        {
            await file.WriteAsync("half"u8.ToArray(), token);
            throw new IOException("the drive is full");
        }, check: null, TimeProvider.System, Ct));

        Assert.AreEqual("old", await File.ReadAllTextAsync(Target, Ct));
        Assert.HasCount(1, FilesBesideTarget());
    }

    [TestMethod]
    public async Task A_cancelled_write_says_it_was_cancelled_even_when_its_temporary_file_cannot_be_removed()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows has no Unix file modes to keep the temporary file in place with");
            return;
        }

        if (Environment.IsPrivilegedProcess)
        {
            Assert.Inconclusive("a privileged process deletes anything");
        }

        await WriteAsync("old");
        string folder = Path.GetDirectoryName(Target)!;
        using CancellationTokenSource cancel = new();
        try
        {
            _ = await Assert.ThrowsAsync<OperationCanceledException>(() => AtomicFile.WriteAsync(Target, async (file, token) =>
            {
                await file.WriteAsync("half"u8.ToArray(), token);
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserExecute);
                }

                await cancel.CancelAsync();
                token.ThrowIfCancellationRequested();
            }, check: null, TimeProvider.System, cancel.Token));
        }
        finally
        {
            File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        Assert.AreEqual("old", await File.ReadAllTextAsync(Target, Ct));
    }

    [TestMethod]
    public async Task The_temporary_file_sits_beside_the_target_under_a_name_the_tool_owns()
    {
        string? seen = null;

        await AtomicFile.WriteAsync(Target, Text("new"), (temporary, _) =>
        {
            seen = temporary;
            return Task.CompletedTask;
        }, TimeProvider.System, Ct);

        Assert.IsNotNull(seen);
        Assert.AreEqual(Path.GetDirectoryName(Target), Path.GetDirectoryName(seen));
        StringAssert.StartsWith(Path.GetFileName(seen), $"file.json.ic-{Environment.ProcessId}-", StringComparison.Ordinal);
        Assert.IsTrue(AtomicFile.IsTemporary(Path.GetFileName(seen)));
        Assert.IsFalse(File.Exists(seen), "the temporary file became the target");
    }

    [TestMethod]
    [DataRow("dxgi.dll")]
    [DataRow("ReShade.ini")]
    [DataRow("setup.tmp")]
    [DataRow("dxgi.dll.tmp")]
    [DataRow("~dxgi.dll.tmp")]
    [DataRow("dxgi.dll.ic")]
    [DataRow("dxgi.dll.ic.tmp")]
    [DataRow("dxgi.dll.ic-12-0123abc.tmp")]
    [DataRow("dxgi.dll.ic-12-0123abcdef.tmp")]
    [DataRow("dxgi.dll.ic-x-0123abcd.tmp")]
    [DataRow("dxgi.dll.ic-12-0123ABCD.tmp")]
    [DataRow("dxgi.dll.ic-12-0123abcd.tmp.bak")]
    [DataRow(".ic-12-0123abcd.tmp")]
    [DataRow("logic-12-0123abcd.tmp")]
    [DataRow("music.ic-12.tmp")]
    [DataRow("dxgi.dll.dlss5-12-0123abcd.tmp")]
    public void A_name_the_tool_did_not_make_is_not_taken_for_its_temporary_file(string fileName) =>
        Assert.IsFalse(AtomicFile.IsTemporary(fileName));

    [TestMethod]
    public void A_leftover_of_a_crashed_write_is_known_as_the_tools_own() =>
        Assert.IsTrue(AtomicFile.IsTemporary("dxgi.dll.ic-4242-0123abcd.tmp"));

    [TestMethod]
    public async Task The_check_reads_the_flushed_content_before_the_target_is_replaced()
    {
        await WriteAsync("old");
        string? checkedContent = null;
        string? targetDuringCheck = null;

        await AtomicFile.WriteAsync(Target, Text("new"), async (temporary, token) =>
        {
            checkedContent = await File.ReadAllTextAsync(temporary, token);
            targetDuringCheck = await File.ReadAllTextAsync(Target, token);
        }, TimeProvider.System, Ct);

        Assert.AreEqual("new", checkedContent);
        Assert.AreEqual("old", targetDuringCheck);
        Assert.AreEqual("new", await File.ReadAllTextAsync(Target, Ct));
    }

    [TestMethod]
    public async Task A_check_that_fails_keeps_the_old_file_and_removes_the_temporary_file()
    {
        await WriteAsync("old");

        _ = await Assert.ThrowsAsync<InvalidDataException>(() => AtomicFile.WriteAsync(
            Target, Text("new"), (_, _) => throw new InvalidDataException("the hash is not the recorded one"), TimeProvider.System, Ct));

        Assert.AreEqual("old", await File.ReadAllTextAsync(Target, Ct));
        Assert.HasCount(1, FilesBesideTarget());
    }
}
