namespace Autopilot.Core.Library;

/// <summary>The executables found under one game folder, best first.</summary>
/// <param name="Candidates">Full paths: every executable scoring above -500 in descending score, or all of them when none does. Ties are broken by the path below the game folder, ignoring case.</param>
/// <param name="FoldersSearched">How many folders the search listed.</param>
/// <param name="StoppedEarly">A cap on executables or folders ended the search, so the folder may hold more.</param>
internal sealed record CandidateSearch(IReadOnlyList<string> Candidates, int FoldersSearched, bool StoppedEarly);
