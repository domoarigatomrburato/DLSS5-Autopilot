using System.Text;
using Autopilot.Core.Storage;

namespace Autopilot.Core.Tests.Storage;

/// <summary>A file replaced whole or not at all.</summary>
[TestClass]
public sealed class AtomicFileTests(TestContext testContext) : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("autopilot-atomic-").FullName;

    private CancellationToken Ct => testContext.CancellationToken;

    private string Target => Path.Join(_folder, "state", "file.json");

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [TestMethod]
    public async Task A_write_creates_the_folder_and_replaces_the_file_whole()
    {
        await WriteAsync("first, and longer");
        await WriteAsync("second");

        Assert.AreEqual("second", await File.ReadAllTextAsync(Target, Ct));
        Assert.HasCount(1, Directory.GetFiles(Path.GetDirectoryName(Target)!));
    }

    [TestMethod]
    public async Task A_write_that_fails_half_way_leaves_the_old_file_and_no_temporary_file()
    {
        await WriteAsync("old");

        _ = await Assert.ThrowsAsync<IOException>(() => AtomicFile.WriteAsync(Target, async (file, token) =>
        {
            await file.WriteAsync("half"u8.ToArray(), token);
            throw new IOException("the drive is full");
        }, Ct));

        Assert.AreEqual("old", await File.ReadAllTextAsync(Target, Ct));
        Assert.HasCount(1, Directory.GetFiles(Path.GetDirectoryName(Target)!));
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
            }, cancel.Token));
        }
        finally
        {
            File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        Assert.AreEqual("old", await File.ReadAllTextAsync(Target, Ct));
    }

    private async Task WriteAsync(string text) =>
        await AtomicFile.WriteAsync(Target, async (file, token) => await file.WriteAsync(Encoding.UTF8.GetBytes(text), token), Ct);
}
