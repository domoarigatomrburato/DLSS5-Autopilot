using Autopilot.Core.Library.Stores;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Tests.Library.Stores;

/// <summary>
/// A machine in a temp folder: well-known folders, drives and game folders below one root, and an
/// in-memory registry. Disposing deletes the tree.
/// </summary>
internal sealed class StoreBed : IDisposable
{
    public StoreBed() => Root = Path.TrimEndingDirectorySeparator(Directory.CreateTempSubdirectory("autopilot-stores-").FullName);

    public string Root { get; }

    public FakeRegistry Registry { get; } = new();

    /// <summary>Every well-known folder set, under the temp root.</summary>
    public HostFolders Folders =>
        new(PathOf("ProgramData"), PathOf("LocalAppData"), PathOf("AppData"), PathOf("Program Files"), PathOf("Program Files (x86)"), PathOf("Desktop"), PathOf("Downloads"), PathOf("Documents"));

    public string PathOf(string relative) => Path.Join(Root, relative);

    /// <summary>Creates a folder and returns its full path.</summary>
    public string Folder(string relative) => Directory.CreateDirectory(PathOf(relative)).FullName;

    /// <summary>Writes a file, creating its folders, and returns its full path.</summary>
    public string WriteFile(string relative, string text = "")
    {
        string path = PathOf(relative);
        TestFiles.Plant(path, text);
        return path;
    }

    /// <summary>A game folder with an executable directly in it.</summary>
    public string Game(string relative, string exe = "game.exe")
    {
        _ = WriteFile(Path.Join(relative, exe));
        return PathOf(relative);
    }

    /// <summary>A drive whose root is a folder of the temp tree.</summary>
    public DriveRoot Drive(string name, DriveKind kind = DriveKind.Fixed) => new(Folder(name) + Path.DirectorySeparatorChar, kind);

    /// <summary>Takes every access away from a folder; the test is inconclusive where that cannot be done.</summary>
    public void Deny(string relative)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows has no Unix file modes to take access away with");
            return;
        }

        if (Environment.IsPrivilegedProcess)
        {
            Assert.Inconclusive("a privileged process reads anything");
        }

        File.SetUnixFileMode(PathOf(relative), UnixFileMode.None);
    }

    /// <summary>A symbolic link at <paramref name="relative"/> to the folder <paramref name="target"/>.</summary>
    public string Link(string relative, string target)
    {
        string path = PathOf(relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _ = Directory.CreateSymbolicLink(path, PathOf(target));
        return path;
    }

    public void Dispose()
    {
        if (!OperatingSystem.IsWindows())
        {
            foreach (string directory in Directory.EnumerateDirectories(Root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0, IgnoreInaccessible = true }).Prepend(Root))
            {
                if (!new DirectoryInfo(directory).Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
            }
        }

        Directory.Delete(Root, recursive: true);
    }

    /// <summary>The games found must have exactly these names, in any order; the failure shows what came back.</summary>
    public static void AssertNames(StoreGame[] games, params string[] expected)
    {
        string[] names = [.. games.Select(game => game.Name)];
        CollectionAssert.AreEquivalent(expected, names, $"got: {string.Join(", ", names)}");
    }

    public static async Task<StoreGame[]> FindAsync(IGameSource source, CancellationToken cancellationToken) =>
        [.. await source.FindAsync(cancellationToken)];
}
