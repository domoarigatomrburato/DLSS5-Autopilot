using Autopilot.Core.Library;
using Autopilot.Core.Tests.Executables;

namespace Autopilot.Core.Tests.Library;

/// <summary>
/// A game folder in a temp directory, named as the test needs (the folder name is part of the
/// ranking), with executables by relative path and size. Sizes are sparse, so a large one costs
/// nothing; a real header comes from <see cref="TestPe"/>. Disposing deletes it.
/// </summary>
internal sealed class GameFolder : IDisposable
{
    private readonly string _parent = Directory.CreateTempSubdirectory("autopilot-games-").FullName;

    /// <summary>The default name matches none of the short executable names the tests use.</summary>
    public GameFolder(string name = "Zq9 Arena")
    {
        Root = Path.Join(_parent, name);
        _ = Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public const long Megabyte = 1024 * 1024;

    public string PathOf(string relative) => Path.Join(Root, relative);

    public GameFolder Folder(string relative)
    {
        _ = Directory.CreateDirectory(PathOf(relative));
        return this;
    }

    /// <summary>An executable of <paramref name="size"/> bytes, with <paramref name="header"/> at its start when given.</summary>
    public GameFolder Exe(string relative, long size = 0, TestPe? header = null)
    {
        string path = PathOf(relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using FileStream file = File.Create(path);
        header?.WriteTo(file);
        if (size > file.Length)
        {
            file.SetLength(size);
        }

        return this;
    }

    /// <summary>An executable of <paramref name="megabytes"/> MB with a header for <paramref name="machine"/>.</summary>
    public GameFolder Pe(string relative, long megabytes, ushort machine) =>
        Exe(relative, megabytes * Megabyte, new TestPe(machine));

    /// <summary>The <c>&lt;name&gt;_Data</c> folder Unity keeps beside its player.</summary>
    public GameFolder UnityData(string playerRelative) => Folder(playerRelative[..^".exe".Length] + "_Data");

    /// <summary>Valve's Source layout: <c>engine.dll</c> under <paramref name="bin"/>, and a game folder holding <c>gameinfo.txt</c>.</summary>
    public GameFolder Source(string bin = "bin", string game = "ep2", bool engine = true, bool gameInfo = true)
    {
        if (engine)
        {
            _ = Exe($"{bin}/engine.dll");
        }

        if (gameInfo)
        {
            File.WriteAllText(Folder(game).PathOf($"{game}/gameinfo.txt"), "\"GameInfo\" {}");
        }

        return this;
    }

    /// <summary>Makes a file or folder unreadable to this process; the test is inconclusive where that cannot be done.</summary>
    public GameFolder Deny(string relative)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows has no Unix file modes to take access away with");
            return this;
        }

        if (Environment.IsPrivilegedProcess)
        {
            Assert.Inconclusive("a privileged process reads anything");
        }

        File.SetUnixFileMode(PathOf(relative), UnixFileMode.None);
        return this;
    }

    public async Task<CandidateSearch> SearchAsync(CancellationToken cancellationToken) =>
        await ExecutableCandidates.RankAsync(Root, cancellationToken);

    /// <summary>The candidates, best first, as paths below the game folder with <c>/</c> separators.</summary>
    public async Task<string[]> RankedAsync(CancellationToken cancellationToken) =>
        Relative(await SearchAsync(cancellationToken));

    public string[] Relative(CandidateSearch search) =>
        [.. search.Candidates.Select(path => Path.GetRelativePath(Root, path).Replace(Path.DirectorySeparatorChar, '/'))];

    public void Dispose()
    {
        UnlockAll(_parent);
        Directory.Delete(_parent, recursive: true);
    }

    // A test that made a folder unreadable leaves it so; deleting needs the access back.
    private static void UnlockAll(string folder)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        foreach (string directory in Directory.EnumerateDirectories(folder, "*", SearchOption.TopDirectoryOnly))
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            UnlockAll(directory);
        }
    }
}
