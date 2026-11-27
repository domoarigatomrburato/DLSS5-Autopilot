namespace Autopilot.Core.Install;

/// <summary>How an uninstall ended: it ran (with or without leftovers), it was refused before changing anything, or an error stopped it.</summary>
public abstract record UninstallOutcome
{
    private protected UninstallOutcome()
    {
    }

    /// <summary>Whether the folder held a transaction an earlier run left open, which this one finished first.</summary>
    public bool Recovered { get; init; }
}

/// <summary>The uninstall ran to its end.</summary>
/// <param name="Removed">How many items it removed or put back.</param>
/// <param name="Lines">A line per item: removed, restored, changed since install, set aside, could not be removed, not removed.</param>
/// <param name="Left">How many items could not be removed or put back: with a record, they stay on it for the next uninstall.</param>
public sealed record UninstallFinished(int Removed, IReadOnlyList<string> Lines, int Left) : UninstallOutcome
{
    /// <summary>Whether there was no usable record, so it cleaned up by known file names.</summary>
    public bool WithoutRecord { get; init; }

    /// <summary>Whether the record stays, still naming changes outside the folder's files that are not undone yet.</summary>
    public bool OutsideKept { get; init; }

    /// <summary>Whether nothing of the install is left.</summary>
    public bool Complete => Left == 0 && !OutsideKept;

    /// <summary>The final message: "Removed N items.", or what is left and what to do.</summary>
    public string Message
    {
        get
        {
            string message = Left == 0 ? Resources.Texts.Format("UninstallDone", Removed) : Resources.Texts.Format("UninstallLeft", Removed, Left);
            return OutsideKept ? message + " " + Resources.Texts.Text("UninstallOutsideKept") : message;
        }
    }
}

/// <summary>The uninstall was refused before anything was changed.</summary>
/// <param name="Reason">Why.</param>
public sealed record UninstallRefused(UninstallRefusal Reason) : UninstallOutcome
{
    /// <summary>Why the folder's record cannot be read, when that is the reason.</summary>
    public RecordProblem? Problem { get; init; }
}

/// <summary>An error stopped the uninstall (the record could not be saved, a transaction left open could not be finished).</summary>
/// <param name="Note">The failure note kept for the folder.</param>
public sealed record UninstallFailed(FailureNote Note) : UninstallOutcome;

/// <summary>Why an uninstall was refused before changing anything.</summary>
public enum UninstallRefusal
{
    /// <summary>A program whose image lies in the install or game folder is running: its files are locked.</summary>
    GameRunning,

    /// <summary>The record is newer than this build, or cannot be read right now: it is not guessed at.</summary>
    RecordUnreadable,

    /// <summary>The record names a game folder outside the one being uninstalled from.</summary>
    RecordOutsideGameFolder,
}
