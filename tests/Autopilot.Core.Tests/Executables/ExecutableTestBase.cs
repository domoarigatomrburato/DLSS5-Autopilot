using Autopilot.Core.Executables;

namespace Autopilot.Core.Tests.Executables;

/// <summary>A temp folder per test, deleted after, and the executables written in it.</summary>
public abstract class ExecutableTestBase(TestContext testContext)
{
    protected string Work { get; private set; } = "";

    protected CancellationToken Ct => testContext.CancellationToken;

    [TestInitialize]
    public void CreateWorkFolder() => Work = Directory.CreateTempSubdirectory("autopilot-tests-").FullName;

    [TestCleanup]
    public void DeleteWorkFolder() => Directory.Delete(Work, recursive: true);

    protected string In(params string[] parts) => Path.Join([Work, .. parts]);

    internal string Write(TestPe pe, string name = "game.exe")
    {
        string path = In(name);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using FileStream file = File.Create(path);
        pe.WriteTo(file);
        return path;
    }

    protected string Write(byte[] bytes, string name = "game.exe")
    {
        string path = In(name);
        TestFiles.Plant(path, bytes);
        return path;
    }

    internal async Task<ReadableExecutable> ReadableAsync(TestPe pe)
    {
        ExecutableReading reading = await Executable.ReadAsync(Write(pe), Ct);
        return Assert.IsInstanceOfType<ReadableExecutable>(reading);
    }

    internal static async Task<PeException> FailsAsync(PeFailure reason, Func<Task> read)
    {
        PeException failure = await Assert.ThrowsExactlyAsync<PeException>(read);
        Assert.AreEqual(reason, failure.Reason, failure.Message);
        return failure;
    }

    protected static void AssertNames(IReadOnlySet<string> names, params string[] expected) =>
        CollectionAssert.AreEquivalent(expected, names.ToList());
}
