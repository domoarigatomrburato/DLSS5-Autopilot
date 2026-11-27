namespace Autopilot.Core.Install;

/// <summary>How an install ended: committed, failed (and rolled back as far as it could), or refused before writing.</summary>
public abstract record InstallOutcome
{
    private protected InstallOutcome()
    {
    }

    /// <summary>Whether the folder held a transaction an earlier run left open, which this one finished first.</summary>
    public bool Recovered { get; init; }
}

/// <summary>The install committed.</summary>
/// <param name="Notes">What the install says, for the report.</param>
/// <param name="Warnings">Its warnings, the files that vanished after being written included.</param>
public sealed record InstallCommitted(IReadOnlyList<string> Notes, IReadOnlyList<string> Warnings) : InstallOutcome;

/// <summary>The install stopped; what it had done was taken back out as far as it could be.</summary>
/// <param name="Note">The failure note kept for the folder.</param>
public sealed record InstallFailed(FailureNote Note) : InstallOutcome
{
    /// <summary>The result card's opening sentence: what became of the folder.</summary>
    public string Opening => Failures.Opening(Note.Operation, Note.Outcome);
}

/// <summary>The install was refused before anything was written.</summary>
/// <param name="Reason">Why.</param>
public sealed record InstallRefused(InstallRefusal Reason) : InstallOutcome
{
    /// <summary>Why the folder's record cannot be read, when that is the reason.</summary>
    public RecordProblem? Problem { get; init; }
}

/// <summary>Why an install was refused before writing.</summary>
public enum InstallRefusal
{
    /// <summary>The folder's record is damaged, the wrong shape, newer than this build, or cannot be read now.</summary>
    RecordUnreadable,

    /// <summary>The folder's record names a game folder outside the one being installed into.</summary>
    RecordOutsideGameFolder,

    /// <summary>The install would touch a place outside the game folder, by its spelling or through a link.</summary>
    PathOutsideGameFolder,

    /// <summary>The install names one path twice, or keeps an entry the previous install does not list.</summary>
    PlanConflict,

    /// <summary>An uninstall left part way could not be finished (the game runs, or something is left): finish the uninstall first.</summary>
    RemovalUnfinished,

    /// <summary>The install registers a layer or sets the graphics preference, and has no registry or layer folder to do it with.</summary>
    SharedPlacesMissing,
}

/// <summary>Where an install stands, for its progress: step <paramref name="Step"/> of <paramref name="Count"/>, counted from 1.</summary>
/// <param name="Step">The step starting.</param>
/// <param name="Count">The steps in all: one per group, plus the journal, the check and the commit.</param>
/// <param name="Name">The step's name.</param>
public readonly record struct InstallProgress(int Step, int Count, string Name);
