namespace Autopilot.Core.Tests.Archives;

/// <summary>A temp folder per test, deleted after, and the archives built in it.</summary>
public abstract class ArchiveTestBase(TestContext testContext)
{
    public static IEnumerable<object[]> Formats =>
        [[ArchiveFormat.Zip], [ArchiveFormat.TarGz], [ArchiveFormat.SevenZip]];

    protected string Work { get; private set; } = "";

    protected CancellationToken Ct => testContext.CancellationToken;

    [TestInitialize]
    public void CreateWorkFolder() => Work = Directory.CreateTempSubdirectory("autopilot-tests-").FullName;

    [TestCleanup]
    public void DeleteWorkFolder() => Directory.Delete(Work, recursive: true);

    protected static void AssertNothingWritten(string destination)
    {
        Assert.IsFalse(Path.Exists(destination), $"{destination} was written");
        AssertNoPart(destination);
    }

    protected static void AssertNoPart(string destination) =>
        Assert.IsFalse(Path.Exists(destination + ".part"), $"{destination}.part was left behind");

    /// <summary>Builds an archive under a name with no extension, so only its bytes tell the format.</summary>
    protected string Build(ArchiveFormat format, params TestMember[] members)
    {
        string path = Path.Join(Work, "archive-" + Guid.NewGuid().ToString("N"));
        TestArchives.Write(format, path, members);
        return path;
    }

    /// <summary>No file named <paramref name="fileName"/> was written anywhere under the work folder.</summary>
    protected void AssertNoFileNamed(string fileName) =>
        Assert.IsEmpty(Directory.GetFiles(Work, fileName, SearchOption.AllDirectories));

    /// <summary>Creates <paramref name="real"/> and a symbolic link to it, the macOS and Linux stand-in for a junction.</summary>
    protected static string LinkTo(string real, string link)
    {
        _ = Directory.CreateDirectory(real);
        _ = Directory.CreateSymbolicLink(link, real);
        return link;
    }

    protected string In(params string[] parts) => Path.Join([Work, .. parts]);
}
