namespace Autopilot.Core.Library;

/// <summary>Finds and ranks the executables of a game folder: the first is the one the tool installs beside.</summary>
internal static class ExecutableCandidates
{
    // Below this a file is dropped from the answer, unless every file is.
    private const double DroppedBelow = -500;

    /// <summary>
    /// Walks the game folder breadth first, scores every <c>.exe</c> and returns them best first.
    /// A folder that does not exist or cannot be listed gives no candidates. The folders are listed
    /// on the calling thread, so call it from a background thread.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task<CandidateSearch> RankAsync(string gameFolder, CancellationToken cancellationToken)
    {
        string folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameFolder));
        var walk = ExecutableWalk.Run(folder, cancellationToken);
        List<Candidate> candidates = [.. walk.Executables.Select(file => Candidate.Read(file, folder)).OfType<Candidate>()];
        ExecutableScore.Rate(candidates, Path.GetFileName(folder));
        await ScoreAdjustments.ApplyAsync(folder, candidates, cancellationToken);
        return new CandidateSearch([.. Ranked(candidates).Select(candidate => candidate.Path)], walk.FoldersSearched, walk.StoppedEarly);
    }

    // A game folder holding only helpers still answers: dropping them all would leave nothing to pick.
    private static List<Candidate> Ranked(List<Candidate> candidates)
    {
        List<Candidate> ordered = [.. candidates.OrderByDescending(candidate => candidate.Score).ThenBy(candidate => candidate.Relative, StringComparer.OrdinalIgnoreCase)];
        List<Candidate> kept = [.. ordered.Where(candidate => candidate.Score > DroppedBelow)];
        return kept.Count > 0 ? kept : ordered;
    }
}
