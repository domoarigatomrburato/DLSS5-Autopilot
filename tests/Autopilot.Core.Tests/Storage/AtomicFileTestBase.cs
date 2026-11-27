using System.Text;
using Autopilot.Core.Storage;

namespace Autopilot.Core.Tests.Storage;

/// <summary>A throwaway folder holding the target of each atomic-write test, deleted after.</summary>
public abstract class AtomicFileTestBase(TestContext testContext)
{
    // Windows: ERROR_SHARING_VIOLATION, ERROR_LOCK_VIOLATION, and ERROR_BAD_NETPATH as a failure that is not a held file.
    protected const int SharingViolation = unchecked((int)0x80070020);
    protected const int LockViolation = unchecked((int)0x80070021);
    protected const int BadNetworkPath = unchecked((int)0x80070035);

    private readonly string _folder = Directory.CreateTempSubdirectory("autopilot-atomic-").FullName;

    protected CancellationToken Ct => testContext.CancellationToken;

    protected string Target => Path.Join(_folder, "state", "file.json");

    /// <summary>The files in the target's folder: the target alone once a write is over.</summary>
    protected string[] FilesBesideTarget() => Directory.GetFiles(Path.GetDirectoryName(Target)!);

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(_folder, recursive: true);

    protected static Func<Stream, CancellationToken, Task> Text(string text) =>
        async (file, token) => await file.WriteAsync(Encoding.UTF8.GetBytes(text), token);

    protected async Task WriteAsync(string text) =>
        await AtomicFile.WriteAsync(Target, Text(text), check: null, TimeProvider.System, Ct);
}
