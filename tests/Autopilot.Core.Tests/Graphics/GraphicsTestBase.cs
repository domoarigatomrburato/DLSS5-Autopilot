using System.Text;
using System.Text.Json;
using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;
using Autopilot.Core.Tests.Executables;

namespace Autopilot.Core.Tests.Graphics;

/// <summary>
/// Game folders built from a few facts: the executable's bitness and imports, the files beside it
/// with their bitness, an install record and its backups, engine layouts and their ini files.
/// </summary>
public abstract class GraphicsTestBase(TestContext testContext) : ExecutableTestBase(testContext)
{
    protected const ushort Bits64 = TestPe.Amd64;
    protected const ushort Bits32 = TestPe.I386;

    /// <summary>The Documents folders the host would pass: the known folder, then its OneDrive variant.</summary>
    protected ApiSearch Search => new([In("Documents"), In("OneDrive", "Documents")]);

    /// <summary>An executable with these static imports.</summary>
    protected string Game(string relative = "Game.exe", ushort machine = Bits64, params string[] imports) =>
        Write(new TestPe(machine).WithImports(imports), relative);

    /// <summary>An executable with these static imports and these names in its bytes, as ANSI.</summary>
    protected string Naming(string relative, ushort machine, string[] imports, params string[] names) =>
        Write(Appending(new TestPe(machine).WithImports(imports), Encoding.Latin1, names), relative);

    /// <summary>A DLL beside the executable (or under its folder) with these static imports.</summary>
    protected void Dll(string exe, string relative, ushort machine = Bits64, params string[] imports) =>
        Write(new TestPe(machine).WithImports(imports), Beside(exe, relative));

    /// <summary>A DLL of at least <paramref name="size"/> bytes naming these renderers in its bytes.</summary>
    protected void NamingDll(string exe, string relative, ushort machine, long size, params string[] names) =>
        Write(Appending(new TestPe(machine) { Size = size }, Encoding.Latin1, names), Beside(exe, relative));

    /// <summary>A file beside the executable that is not an executable at all.</summary>
    protected static void Junk(string exe, string relative, string content = "MZ") =>
        TestFiles.Plant(Path.Join(Path.GetDirectoryName(exe), relative), content);

    /// <summary>This tool's install record beside the executable, listing these files as written.</summary>
    protected static void Record(string exe, params string[] files) =>
        Junk(exe, "dlss5-autopilot.json", JsonSerializer.Serialize(new { files }));

    protected static void Folder(params string[] parts) => Directory.CreateDirectory(Path.Join(parts));

    protected async Task<ApiDecision> DecideAsync(string exe) =>
        await GraphicsApiDecision.DecideAsync(exe, await Executable.ReadAsync(exe, Ct), Search, Ct);

    /// <summary>Checks the API, the kind of evidence, and that the words name the evidence.</summary>
    protected static void Is(ApiDecision decision, GraphicsApi api, ApiReason reason, params string[] words)
    {
        Assert.AreEqual(api, decision.Api, decision.Explanation);
        Assert.AreEqual(reason, decision.Reason, decision.Explanation);
        foreach (string word in words)
        {
            Assert.Contains(word, decision.Explanation);
        }
    }

    /// <summary>Takes away the right to read a file, as Windows refuses a protected one; inconclusive where permissions do not bind.</summary>
    protected static void Refuse(string path)
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

    internal static TestPe Appending(TestPe pe, Encoding encoding, params string[] names)
    {
        foreach (string name in names)
        {
            _ = pe.Append("\0" + name + "\0", encoding);
        }

        return pe;
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

    private string Beside(string exe, string relative) => Path.GetRelativePath(Work, Path.Join(Path.GetDirectoryName(exe), relative));
}
