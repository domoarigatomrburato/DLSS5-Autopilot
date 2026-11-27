using Autopilot.Core.Library;
using Autopilot.Core.Tests.Executables;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Library;

/// <summary>
/// A game folder on a <see cref="GameTree"/>, named as the test needs (the folder name is part of the
/// ranking), with the Library tests' extras: executables by relative path and size, Unity's and
/// Source's layouts, access taken away, and the candidate ranking. Sizes are sparse, so a large one
/// costs nothing; a real header comes from <see cref="TestPe"/>. Disposing deletes it.
/// </summary>
/// <param name="name">The folder's name; the default matches none of the short executable names the tests use.</param>
internal sealed class GameFolder(string name = "Zq9 Arena") : IDisposable
{
    private readonly GameTree _tree = new(name);

    public string Root => _tree.Root;

    public const long Megabyte = 1024 * 1024;

    /// <summary>The full path of <paramref name="relative"/>, spelled with either slash.</summary>
    public string PathOf(string relative) => _tree.PathOf(relative);

    public GameFolder Folder(string relative)
    {
        _ = _tree.Folder(relative);
        return this;
    }

    /// <summary>An executable of <paramref name="size"/> bytes, with <paramref name="header"/> at its start when given.</summary>
    public GameFolder Exe(string relative, long size = 0, TestPe? header = null)
    {
        _ = _tree.Add(new TreeFile(relative) { Content = header?.ToBytes(), Size = size });
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
            _ = _tree.Add(TreeFile.Text($"{game}/gameinfo.txt", "\"GameInfo\" {}"));
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

    public void Dispose() => _tree.Dispose();
}
