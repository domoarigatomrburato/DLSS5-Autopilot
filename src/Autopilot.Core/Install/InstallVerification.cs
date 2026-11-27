namespace Autopilot.Core.Install;

/// <summary>
/// "Verify": the record compared with the disk, entry by entry, so the game page and the diagnosis see a file
/// removed after the install (antivirus, a store's repair) as well as within it. Reads only: it writes nothing
/// and takes no lock beyond reading the record.
/// </summary>
/// <param name="records">Reads the record.</param>
public sealed class InstallVerification(IInstallRecords records)
{
    /// <summary>
    /// Each entry the record in <paramref name="installFolder"/> places or moves aside, against the disk. An entry
    /// whose path leads outside the game folder is left out, never followed. The file is hashed on the calling
    /// thread: call it from a background thread.
    /// </summary>
    /// <exception cref="IOException">A file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">A file cannot be opened.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public async Task<RecordVerification> VerifyAsync(string installFolder, CancellationToken cancellationToken)
    {
        RecordedInstall? recorded = await records.FindAsync(installFolder, cancellationToken);
        if (recorded is null)
        {
            return new(VerifiedRecord.None, null, []);
        }

        if (recorded is not { Record: { } record, GameFolder: { } gameFolder })
        {
            return new(VerifiedRecord.Unreadable, recorded.Problem, []);
        }

        InstallPaths paths = new(gameFolder);
        OwnedFiles owned = new(recorded);
        List<VerifiedEntry> entries = [];
        foreach (RecordEntry entry in record.Placed)
        {
            string full = paths.Full(entry.Path);
            if (paths.Contains(full))
            {
                entries.Add(await EntryAsync(entry, full, owned, cancellationToken));
            }
        }

        return new(VerifiedRecord.Read, null, entries) { State = recorded.State };
    }

    private static async Task<VerifiedEntry> EntryAsync(RecordEntry entry, string full, OwnedFiles owned, CancellationToken cancellationToken)
    {
        bool? backupValid = entry.Original is null ? null : await owned.IsBackupValidAsync(entry, cancellationToken);
        return new(entry.Path, entry.Class, await FileStateAsync(entry, full, owned, cancellationToken), backupValid);
    }

    // An entry an open record has not written yet is no file to look at; a file moved aside is in order when nothing
    // is back at its path; ours, when it is still ours (a config file whenever it is there).
    private static async Task<VerifiedFile> FileStateAsync(RecordEntry entry, string full, OwnedFiles owned, CancellationToken cancellationToken) =>
        (entry.State, File.Exists(full)) switch
        {
            (EntryState.Pending, _) => VerifiedFile.NotWritten,
            (EntryState.MovedAside, false) => VerifiedFile.Ok,
            (EntryState.MovedAside, true) => VerifiedFile.Changed,
            (_, false) => VerifiedFile.Missing,
            _ => await owned.IsOursAsync(full, cancellationToken) ? VerifiedFile.Ok : VerifiedFile.Changed,
        };
}

/// <summary>The record of a folder compared with the disk.</summary>
/// <param name="Record">Whether there was a record, and whether it could be read.</param>
/// <param name="Problem">Why the record cannot be read; null when it can, or there is none.</param>
/// <param name="Entries">Each entry the record places or moves aside; empty unless <see cref="VerifiedRecord.Read"/>.</param>
public sealed record RecordVerification(VerifiedRecord Record, RecordProblem? Problem, IReadOnlyList<VerifiedEntry> Entries)
{
    /// <summary>Where the recorded install stands, when the record was read.</summary>
    public InstallState? State { get; init; }

    /// <summary>
    /// Whether every entry written is in order (nothing gone or changed since), with every backup it relies on valid;
    /// entries an open record has not written yet are not counted: <see cref="State"/> says the record is open.
    /// </summary>
    public bool AllOk => Entries.All(entry => entry is { File: VerifiedFile.Ok or VerifiedFile.NotWritten, BackupValid: not false });
}

/// <summary>Whether a folder had a record, and whether it could be read.</summary>
public enum VerifiedRecord
{
    /// <summary>No record: nothing of ours is recorded here.</summary>
    None,

    /// <summary>A record that cannot be read: damaged, the wrong shape, newer, or unreadable now.</summary>
    Unreadable,

    /// <summary>The record was read and compared.</summary>
    Read,
}

/// <summary>One entry of the record, against the disk.</summary>
/// <param name="Path">The entry's path, relative to the game folder.</param>
/// <param name="Class">Whether the file is expected to stay as written.</param>
/// <param name="File">What is at the path.</param>
/// <param name="BackupValid">For an entry over a game's original: whether its backup is there with exactly the recorded bytes; null when there is no original.</param>
public sealed record VerifiedEntry(string Path, EntryClass Class, VerifiedFile File, bool? BackupValid);

/// <summary>What is at an entry's path, against the record.</summary>
public enum VerifiedFile
{
    /// <summary>As recorded: the written bytes (any content for a config file), or nothing where a file was moved aside.</summary>
    Ok,

    /// <summary>No file where the record placed one: antivirus, almost always.</summary>
    Missing,

    /// <summary>Other bytes than written, or a file back where one was moved aside.</summary>
    Changed,

    /// <summary>Not written yet: the record is open (an install part way), and nothing is expected at the path.</summary>
    NotWritten,
}
