using Autopilot.Core.Resources;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install;

/// <summary>
/// The journal, the groups, the check and the commit of one install, as resolved. Before each group every
/// path it touches is on the saved record; after it, its entries are settled and the record saved again.
/// </summary>
/// <param name="disk">Makes the changes.</param>
/// <param name="installFolder">Where the record is.</param>
/// <param name="logger">Where vanished files and the commit's tail are logged.</param>
/// <param name="outside">Releases at commit what the previous install relied on outside the game folder and this one does not.</param>
internal sealed partial class TransactionApply(InstallDisk disk, string installFolder, ILogger logger, IOutsideChanges outside)
{
    private const int LoggedVanished = 8;
    private const int NamedVanished = 4;

    /// <summary>The record last saved: what a rollback starts from; null while none was.</summary>
    public InstallRecord? Saved { get; private set; }

    /// <summary>Whether any change other than a record save went ahead.</summary>
    public bool Changed => disk.Changed;

    /// <summary>Applies <paramref name="resolved"/> and commits it.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled before the commit.</exception>
    public async Task<InstallCommitted> RunAsync(ResolvedInstall resolved, IProgress<InstallProgress>? progress, CancellationToken cancellationToken)
    {
        int step = 0;
        void Next(string name) => progress?.Report(new(++step, resolved.ProgressSteps, name));

        Next(Texts.Text("InstallStepJournal"));
        InstallRecord record = resolved.Opening;
        await SaveAsync(record, cancellationToken);
        foreach (ResolvedGroup group in resolved.Groups)
        {
            Next(group.Name);
            record = await GroupAsync(resolved.Paths, group, record, cancellationToken);
        }

        Next(Texts.Text("InstallStepVerify"));
        IReadOnlyList<string> vanished = await VanishedAsync(resolved, cancellationToken);
        Next(Texts.Text("InstallStepCommit"));
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<string> warnings = vanished.Count == 0 ? record.Warnings : [.. record.Warnings, Quarantined(vanished)];
        // The originals put back are the game's again: nothing of them stays on the record.
        record = Releases.Committed(record) with
        {
            Warnings = warnings,
            Entries = [.. record.Entries.Where(entry => entry.State != EntryState.Restored)],
        };
        await SaveAsync(record, cancellationToken);
        await FinishCommitAsync(record, resolved.Paths);
        return new InstallCommitted(record.Notes, warnings);
    }

    private async Task<InstallRecord> GroupAsync(InstallPaths paths, ResolvedGroup group, InstallRecord record, CancellationToken cancellationToken)
    {
        if (group.Actions.Count == 0)
        {
            return record;
        }

        cancellationToken.ThrowIfCancellationRequested();
        record = group.JournaledOnto(record);
        await SaveAsync(record, cancellationToken);
        foreach (InstallAction action in group.Actions)
        {
            record = await ActionAsync(paths, action, record, cancellationToken);
        }

        record = record with { Entries = [.. record.Entries.Select(Settled)] };
        await SaveAsync(record, cancellationToken);
        return record;
    }

    // An action's changes, in order; a failure the action tolerates ends them and is accounted for on the record.
    private async Task<InstallRecord> ActionAsync(InstallPaths paths, InstallAction action, InstallRecord record, CancellationToken cancellationToken)
    {
        InstallRecord? tolerated = null;
        try
        {
            foreach (DiskChange change in action.Changes(paths))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await disk.RunAsync(change, cancellationToken);
            }
        }
        catch (Exception failure) when (failure is not OperationCanceledException && (tolerated = action.Tolerate(record, failure)) is not null)
        {
            LogTolerated(logger, failure, action.Path);
        }

        return tolerated ?? record;
    }

    // A pending entry, once its group ran, is written, or moved aside when nothing was written at it.
    private static RecordEntry Settled(RecordEntry entry) =>
        entry.State != EntryState.Pending ? entry
        : entry with { State = entry.Written is null ? EntryState.MovedAside : EntryState.Written };

    private async Task SaveAsync(InstallRecord record, CancellationToken cancellationToken)
    {
        await disk.SaveRecordAsync(installFolder, record, cancellationToken);
        Saved = record;
    }

    // The component files this run wrote, not there with the size and hash written: antivirus, almost always.
    private async Task<IReadOnlyList<string>> VanishedAsync(ResolvedInstall resolved, CancellationToken cancellationToken)
    {
        List<string> vanished = [];
        foreach (RecordEntry entry in resolved.Journaled.Where(entry => entry is { Class: EntryClass.Component, State: EntryState.Pending, Written: not null }))
        {
            if (!await entry.Written!.IsAtAsync(resolved.Paths.Full(entry.Path), cancellationToken))
            {
                vanished.Add(entry.Path);
            }
        }

        foreach (string path in vanished.Take(LoggedVanished))
        {
            LogVanished(logger, path);
        }

        return vanished;
    }

    private static string Quarantined(IReadOnlyList<string> vanished) =>
        Texts.Format("InstallQuarantined", vanished.Count, string.Join(", ", vanished.Take(NamedVanished)) + (vanished.Count > NamedVanished ? "..." : ""));

    // Committed: what the previous install relied on outside the folder is released, the held files go, and the record
    // is saved without them. Nothing here fails the install: what is left, the next transaction on the folder finishes first.
    private async Task FinishCommitAsync(InstallRecord record, InstallPaths paths)
    {
        try
        {
            record = await Releases.ReleaseAsync(record, disk, installFolder, outside);
            if (record.Entries.Any(HeldFiles.IsHeld))
            {
                _ = await HeldFiles.DeleteAsync(record, paths, disk, installFolder, logger);
            }
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            LogCommitTail(logger, failure, installFolder);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Path} could not be set; the install goes on without it")]
    private static partial void LogTolerated(ILogger logger, Exception failure, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Path} was written and is no longer there as written")]
    private static partial void LogVanished(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The install in {Folder} committed, but deleting its held files stopped; the next install of the folder finishes it")]
    private static partial void LogCommitTail(ILogger logger, Exception failure, string folder);
}

/// <summary>The files held until commit: deleted once it commits.</summary>
internal static partial class HeldFiles
{
    /// <summary>Whether <paramref name="entry"/> is a held file's.</summary>
    public static bool IsHeld(RecordEntry entry) => entry.State == EntryState.Held;

    /// <summary>The commit's change for the held <paramref name="entry"/>: its held file deleted.</summary>
    public static DeleteFile DeleteOf(RecordEntry entry, InstallPaths paths) => new(InstallPaths.HeldOf(paths.Full(entry.Path)));

    /// <summary>
    /// Deletes every held file of the committed <paramref name="record"/> and saves it without their entries;
    /// a file another program holds keeps its entry. Never cancelled: the install has committed.
    /// </summary>
    /// <returns>The first held file another program kept, with the error Windows gave; null when every one went.</returns>
    public static async Task<FileHeldException?> DeleteAsync(InstallRecord record, InstallPaths paths, InstallDisk disk, string installFolder, ILogger logger)
    {
        List<RecordEntry> left = [];
        FileHeldException? first = null;
        foreach (RecordEntry entry in record.Entries)
        {
            FileHeldException? kept = IsHeld(entry) ? await TryDeleteAsync(disk, DeleteOf(entry, paths), logger) : null;
            if (!IsHeld(entry) || kept is not null)
            {
                left.Add(entry);
            }

            first ??= kept;
        }

        await disk.SaveRecordAsync(installFolder, record with { Entries = left }, CancellationToken.None);
        return first;
    }

    // Gone already (a commit finished half way) is deleted.
    private static async Task<FileHeldException?> TryDeleteAsync(InstallDisk disk, DeleteFile delete, ILogger logger)
    {
        try
        {
            if (File.Exists(delete.Path))
            {
                await disk.RunAsync(delete, CancellationToken.None);
            }

            return null;
        }
        catch (FileHeldException held)
        {
            LogHeldLeft(logger, delete.Path);
            return held;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The held file {Path} could not be deleted; the next install of the folder deletes it")]
    private static partial void LogHeldLeft(ILogger logger, string path);
}

/// <summary>What a committed install releases of the one it replaced: the registrations and graphics preferences it no longer relies on.</summary>
internal static class Releases
{
    /// <summary>The committed form of <paramref name="record"/>: installed, the previous install dropped, and what it releases of it kept until it is.</summary>
    public static InstallRecord Committed(InstallRecord record) => record with { State = RecordState.Installed, Previous = null, Releasing = Of(record) };

    /// <summary>What <paramref name="record"/>, about to commit, releases of its previous install; null when nothing.</summary>
    public static SharedRelease? Of(InstallRecord record) => record.Previous is { } previous ? Leftover(previous, record) : null;

    /// <summary>
    /// What <paramref name="from"/> made outside its files, and still had to release, that <paramref name="kept"/> does not rely
    /// on: the registrations it does not list, and the graphics preferences of programs it holds none for, compared as resolved
    /// paths; null when nothing.
    /// </summary>
    public static SharedRelease? Leftover(InstallRecord from, InstallRecord kept)
    {
        LayerRegistration[] registrations =
        [
            .. from.Registrations.Concat(from.Releasing?.Registrations ?? [])
                .Where(registration => !LayerDependents.Names(kept.Registrations, registration)),
        ];
        GraphicsPreferenceEdit[] preferences =
        [
            .. from.GraphicsPreferences.Concat(from.Releasing?.GraphicsPreferences ?? [])
                .Where(edit => !kept.GraphicsPreferences.Any(held => SharedPaths.Same(held.Program, edit.Program))),
        ];
        return registrations.Length + preferences.Length > 0 ? new(registrations, preferences) : null;
    }

    /// <summary>How many registrations and graphics preferences <paramref name="release"/> holds; none when null.</summary>
    public static int Count(SharedRelease? release) => release is null ? 0 : release.Registrations.Count + release.GraphicsPreferences.Count;

    /// <summary>
    /// Releases what the committed <paramref name="record"/> still has to, and saves it with only what could not be released
    /// left (none: no <see cref="InstallRecord.Releasing"/>); unsaved when nothing could be released.
    /// </summary>
    public static async Task<InstallRecord> ReleaseAsync(InstallRecord record, InstallDisk disk, string installFolder, IOutsideChanges outside)
    {
        if (record.Releasing is not { } releasing)
        {
            return record;
        }

        SharedRelease? left = await outside.UndoAsync(releasing);
        if (left is not null && Count(left) == Count(releasing))
        {
            return record;
        }

        record = record with { Releasing = left };
        await disk.SaveRecordAsync(installFolder, record, CancellationToken.None);
        return record;
    }
}
