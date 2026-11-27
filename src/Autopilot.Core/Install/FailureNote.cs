using Autopilot.Core.Net;
using Autopilot.Core.Platform;
using Autopilot.Core.Routes;
using Autopilot.Core.State;
using Autopilot.Core.Storage;

namespace Autopilot.Core.Install;

/// <summary>
/// Why the last install or uninstall of a folder stopped, kept in the game's per-game state (never in
/// the game folder) until the next install or uninstall of that folder starts.
/// </summary>
/// <param name="When">The time it stopped.</param>
/// <param name="Operation">What stopped.</param>
/// <param name="Kind">What stopped it.</param>
/// <param name="Cause">The detail behind <paramref name="Kind"/>, when it has one.</param>
/// <param name="Text">The message the person was shown; kept up to 400 characters.</param>
/// <param name="Outcome">What the folder was left as.</param>
/// <param name="Route">What was being installed or removed; null when not known.</param>
/// <param name="ToolVersion">The build that ran it.</param>
/// <param name="Exception">The last 4,000 characters of an unexpected exception, for the bug report only; nothing parses it.</param>
public sealed record FailureNote(
    DateTimeOffset When,
    FailureOperation Operation,
    FailureKind Kind,
    FailureCause? Cause,
    string Text,
    FailureOutcome Outcome,
    RouteId? Route,
    string ToolVersion,
    string? Exception);

/// <summary>The operation a failure note is about.</summary>
public enum FailureOperation
{
    Install,
    Uninstall,

    /// <summary>The rollback of a failed install.</summary>
    Rollback,
}

/// <summary>What stopped an operation.</summary>
public enum FailureKind
{
    /// <summary>A check refused it before anything was written.</summary>
    Refused,
    Network,
    DiskFull,
    Locked,
    Verification,
    Cancelled,
    Unexpected,
}

/// <summary>What the folder was left as.</summary>
public enum FailureOutcome
{
    NothingWritten,
    RolledBack,

    /// <summary>The rollback could not undo everything.</summary>
    RollbackIncomplete,
}

/// <summary>The detail behind a failure: for a network failure its kind and host, for a full drive the drives, for a locked file its path.</summary>
/// <param name="Network">The network failure's kind (<c>blocked</c>, <c>dns</c>, <c>refused</c>, <c>timeout</c>, <c>cut</c> ...).</param>
/// <param name="Host">The host the network failure was at.</param>
/// <param name="Drives">The drives involved in a full-drive failure, with their free space.</param>
/// <param name="Path">The locked file.</param>
public sealed record FailureCause(NetErrorKind? Network = null, string? Host = null, IReadOnlyList<DrivePlace>? Drives = null, string? Path = null);

/// <summary>Reads, keeps and clears a game's failure note in the per-game state store, under one key.</summary>
public static class FailureNotes
{
    /// <summary>The per-game state key the note lives under.</summary>
    internal const string Key = "failure";

    private const int MaxText = 400;
    private const int MaxException = 4_000;

    /// <summary>The game's failure note; null when it has none, or the value there is not one.</summary>
    public static FailureNote? Read(GameStateStore store, GameIdentity game)
    {
        ArgumentNullException.ThrowIfNull(store);
        return store.Read(game).Get(Key, RecordJson.Note);
    }

    /// <summary>Keeps <paramref name="note"/> as the game's failure note, its text cut to 400 characters and its exception to the last 4,000.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static void Keep(GameStateStore store, GameIdentity game, FailureNote note, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(note);
        FailureNote kept = note with
        {
            Text = Head(note.Text, MaxText),
            Exception = note.Exception is { } exception ? Tail(exception, MaxException) : null,
        };
        store.Update(game, state => state.Set(Key, kept, RecordJson.Note), cancellationToken);
    }

    // At most the first max characters, never half of a character made of two (an emoji).
    private static string Head(string text, int max)
    {
        if (text.Length <= max)
        {
            return text;
        }

        return text[..(char.IsHighSurrogate(text[max - 1]) ? max - 1 : max)];
    }

    // At most the last max characters, never half of a character made of two.
    private static string Tail(string text, int max)
    {
        if (text.Length <= max)
        {
            return text;
        }

        int start = text.Length - max;
        return text[(char.IsLowSurrogate(text[start]) ? start + 1 : start)..];
    }

    /// <summary>Clears the game's failure note, as the next install or uninstall of its folder starts.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static void Clear(GameStateStore store, GameIdentity game, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        store.Update(game, state => state.Remove(Key), cancellationToken);
    }
}
