using Autopilot.Core.Resources;

namespace Autopilot.Core.Library;

/// <summary>The library a scan found, sorted by name, and what the log should say about it.</summary>
public sealed record LibraryScan(IReadOnlyList<GameEntry> Games, IReadOnlyList<LibraryNote> Notes)
{
    /// <summary>How many games are listed without an executable.</summary>
    public int FoldersWithoutExecutable => Games.Count(game => game.Error?.Failure == GameReadFailure.NoExecutable);

    /// <summary>"N folders had no executable", or null when every game has one.</summary>
    public string? ExplainFoldersWithoutExecutable() => FoldersWithoutExecutable switch
    {
        0 => null,
        1 => Texts.Text("ScanOneWithoutExecutable"),
        var count => Texts.Format("ScanManyWithoutExecutable", count),
    };
}
