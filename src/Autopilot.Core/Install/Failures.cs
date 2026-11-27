using Autopilot.Core.Platform;
using Autopilot.Core.Resources;
using Autopilot.Core.Routes;
using Autopilot.Core.State;
using Autopilot.Core.Storage;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install;

/// <summary>Turns a failed install of one folder into its failure note, kept in the game's state, and its result.</summary>
/// <param name="store">Keeps the note.</param>
/// <param name="space">Names the drives of a full-drive failure.</param>
/// <param name="time">The note's clock.</param>
/// <param name="logger">Where the failure is logged.</param>
/// <param name="subject">What failed, where.</param>
internal sealed partial class Failures(GameStateStore store, IFreeSpace space, TimeProvider time, ILogger logger, FailureSubject subject)
{
    /// <summary>What failed, where.</summary>
    public FailureSubject Subject => subject;

    /// <summary>The result card's opening sentence: what <paramref name="operation"/> left of the folder.</summary>
    public static string Opening(FailureOperation operation, FailureOutcome outcome) => Texts.Text((operation, outcome) switch
    {
        (FailureOperation.Uninstall, FailureOutcome.NothingWritten) => "UninstallOpeningNothingChanged",
        (FailureOperation.Uninstall, _) => "UninstallOpeningStopped",
        (_, FailureOutcome.NothingWritten) => "InstallOpeningNothingWritten",
        (_, FailureOutcome.RolledBack) => "InstallOpeningRolledBack",
        _ => "InstallOpeningIncomplete",
    });

    /// <summary>Clears the folder's note, as an install starts.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public void ClearNote(CancellationToken cancellationToken) => FailureNotes.Clear(store, subject.Game, cancellationToken);

    /// <summary>Keeps the note for <paramref name="failure"/> and gives the result the person sees.</summary>
    public InstallFailed Failed(FailureOperation operation, Exception failure, FailureOutcome outcome) =>
        Keep(operation, NoteCauses.Describe(failure, subject.GameFolder, space), outcome, failure);

    /// <summary>Keeps the note for an uninstall <paramref name="failure"/> stopped, and gives the note.</summary>
    /// <param name="failure">What stopped it.</param>
    /// <param name="changed">Whether it had changed anything before it stopped.</param>
    public FailureNote UninstallStopped(Exception failure, bool changed)
    {
        NoteCause cause = NoteCauses.Describe(failure, subject.GameFolder, space);
        // A refused registry change and a held layer file keep their own words.
        if (cause.Kind is FailureKind.Locked or FailureKind.Unexpected && ExceptionChain.Find<ExplainedException>(failure) is null)
        {
            cause = cause with { Text = Texts.Format("UninstallError", ExceptionChain.Of(failure).Last().Message) };
        }

        FailureOutcome outcome = changed ? FailureOutcome.RollbackIncomplete : FailureOutcome.NothingWritten;
        return Keep(FailureOperation.Uninstall, cause, outcome, failure).Note;
    }

    /// <summary>Keeps the note for a rollback that stopped at <paramref name="stop"/> and gives the result the person sees.</summary>
    public InstallFailed Failed(FailureOperation operation, RollbackStop stop, FailureOutcome outcome) =>
        stop.Error is { } error
            ? Failed(operation, error, outcome)
            : Keep(operation, new(FailureKind.Unexpected, new FailureCause(Path: stop.Path), Texts.Format("InstallNotPutBack", Path.GetFileName(stop.Path))), outcome, null);

    private InstallFailed Keep(FailureOperation operation, NoteCause cause, FailureOutcome outcome, Exception? failure)
    {
        Log(operation, failure, cause.Kind, outcome);
        FailureNote note = new(
            time.GetUtcNow(),
            operation,
            cause.Kind,
            cause.Detail,
            Opening(operation, outcome) + "\n\n" + cause.Text,
            outcome,
            subject.Route,
            subject.ToolVersion,
            cause.Kind == FailureKind.Unexpected ? failure?.ToString() : null);
        // Kept even after a cancellation: the note is what the next start reads.
        FailureNotes.Keep(store, subject.Game, note, CancellationToken.None);
        return new InstallFailed(note);
    }

    // A cancellation is never logged as an error.
    private void Log(FailureOperation operation, Exception? failure, FailureKind kind, FailureOutcome outcome)
    {
        if (kind == FailureKind.Cancelled)
        {
            LogCancelled(logger, subject.InstallFolder, outcome);
        }
        else
        {
            LogFailed(logger, failure, subject.InstallFolder, operation, kind, outcome);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The install in {Folder} was cancelled ({Outcome})")]
    private static partial void LogCancelled(ILogger logger, string folder, FailureOutcome outcome);

    [LoggerMessage(Level = LogLevel.Error, Message = "The {Operation} in {Folder} failed ({Kind}, {Outcome})")]
    private static partial void LogFailed(ILogger logger, Exception? failure, string folder, FailureOperation operation, FailureKind kind, FailureOutcome outcome);
}

/// <summary>How a failure reads on its note.</summary>
internal static class NoteCauses
{
    /// <summary>
    /// The kind, the detail and the cause paragraph, however the failure was wrapped: a full drive with the drives
    /// involved, a held file with the error Windows gave, a backup that could not be made, a cancellation.
    /// </summary>
    public static NoteCause Describe(Exception failure, string gameFolder, IFreeSpace space)
    {
        NoteCause cause = failure switch
        {
            OperationCanceledException => new(FailureKind.Cancelled, null, Texts.Text("InstallCancelled")),
            _ when ExceptionChain.Find<DiskFullException>(failure) is { } full =>
                new(FailureKind.DiskFull, new FailureCause(Drives: full.Places(gameFolder, space)), full.Explain(gameFolder, space)),
            _ when ExceptionChain.Find<ExplainedException>(failure) is { } explained => new(explained.Kind, explained.Cause, explained.Text),
            _ when ExceptionChain.Find<FileHeldException>(failure) is { } held =>
                new(FailureKind.Locked, new FailureCause(Path: held.FilePath), Texts.Format("InstallWriteRefused", Innermost(held).Message)),
            _ when ExceptionChain.Find<NotVerifiedException>(failure) is { } unverified =>
                new(FailureKind.Verification, null, Texts.Format("InstallNotVerified", Path.GetFileName(unverified.FilePath))),
            _ => new(FailureKind.Unexpected, null, Texts.Format("InstallUnexpected", failure.Message)),
        };
        return ExceptionChain.Find<BackupFailedException>(failure) is { } backup
            ? cause with { Text = Texts.Format("InstallBackupFailed", Path.GetFileName(backup.FilePath), Innermost(backup).Message) }
            : cause;
    }

    private static Exception Innermost(Exception failure) => ExceptionChain.Of(failure).Last();
}

/// <summary>A failure as the note keeps it: the cause value every note is built from.</summary>
/// <param name="Kind">What stopped the install.</param>
/// <param name="Detail">The drives, or the held file.</param>
/// <param name="Text">The cause paragraph, which follows the opening sentence.</param>
internal sealed record NoteCause(FailureKind Kind, FailureCause? Detail, string Text);

/// <summary>What a failure note is about.</summary>
/// <param name="GameFolder">The game folder.</param>
/// <param name="InstallFolder">The install folder, where the record is.</param>
/// <param name="Route">What was being installed; null when not known.</param>
/// <param name="ToolVersion">The build running.</param>
internal sealed record FailureSubject(string GameFolder, string InstallFolder, RouteId? Route, string ToolVersion)
{
    /// <summary>The game whose note it is.</summary>
    public GameIdentity Game { get; } = GameIdentity.Of(InstallFolder);
}
