using Autopilot.Core.Executables;

namespace Autopilot.Core.Tests.Executables;

/// <summary>
/// A store game's executable that Windows refuses to open while its folder is readable; on macOS
/// and Linux, a file whose permissions refuse reading stands in for it.
/// </summary>
[TestClass]
public sealed class ProtectedExecutableTests(TestContext testContext) : ExecutableTestBase(testContext)
{
    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    public async Task A_listed_executable_whose_content_is_refused_is_protected()
    {
        string path = Write(new TestPe());
        RefuseOrSkip(path);

        ExecutableReading reading = await Executable.ReadAsync(path, Ct);

        _ = Assert.IsInstanceOfType<ProtectedExecutable>(reading);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX)]
    public async Task A_refused_file_has_no_version_and_cannot_be_read_for_it()
    {
        string path = Write(new TestPe());
        RefuseOrSkip(path);

        PeException failure = await FailsAsync(PeFailure.Unreadable, () => Executable.FileVersionAsync(path, Ct));

        Assert.IsInstanceOfType<UnauthorizedAccessException>(failure.InnerException);
    }

    private static void RefuseOrSkip(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.None);
        }

        if (CanOpen(path))
        {
            Assert.Inconclusive("running as root, where file permissions do not bind");
        }
    }

    private static bool CanOpen(string path)
    {
        try
        {
            File.OpenRead(path).Dispose();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
