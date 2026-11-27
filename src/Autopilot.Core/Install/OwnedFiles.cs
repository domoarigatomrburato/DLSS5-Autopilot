using Autopilot.Core.Archives;
using Autopilot.Core.Storage;

namespace Autopilot.Core.Install;

/// <summary>
/// Which files are this tool's, by the install record as <see cref="InstallRecords"/> read it: the one
/// home of that question. A missing or unreadable record means nothing is ours: a damaged record is
/// never guessed at.
/// </summary>
internal sealed class OwnedFiles
{
    private readonly string? _gameFolder;
    private readonly Dictionary<string, RecordEntry> _entries = [with(StringComparer.OrdinalIgnoreCase)];

    /// <param name="recorded">The record found by <see cref="InstallRecords"/>, or null: its entries are relative to the game folder it derived.</param>
    public OwnedFiles(RecordedInstall? recorded)
    {
        if (recorded is not { Record: { } record, GameFolder: { } gameFolder })
        {
            return;
        }

        _gameFolder = gameFolder;
        foreach (RecordEntry entry in record.Entries)
        {
            // The reader kept the first of two entries naming one path; so does this.
            _ = _entries.TryAdd(RecordPaths.Key(entry.Path), entry);
        }
    }

    /// <summary>
    /// Whether the record lists the file at <paramref name="path"/> as written by this tool over nothing
    /// of the game's: the cheap check the graphics decision and the runtime reader make. A file whose
    /// original the record keeps (a swap), or one moved aside, is the game's.
    /// </summary>
    public bool IsListedAsOurs(string path) => Listed(path) is { Original: null };

    /// <summary>Whether the record lists the file at <paramref name="path"/> as one the tool wrote, over the game's own or not.</summary>
    public bool IsListed(string path) => Listed(path) is not null;

    /// <summary>
    /// Whether the file at <paramref name="path"/> is ours, as install decides before it overwrites, moves
    /// or deletes it: listed as written, and for a component its current content hashes to what was
    /// written (a game update may have replaced it). A config file listed is ours whatever its content;
    /// a missing file is not ours.
    /// </summary>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The file cannot be opened.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public async Task<bool> IsOursAsync(string path, CancellationToken cancellationToken)
    {
        FileInfo file = new(path);
        if (Listed(path) is not { } entry || !file.Exists)
        {
            return false;
        }

        return entry.Class == EntryClass.Config
            || (entry.Written is { } written && await HoldsAsync(file, written, cancellationToken));
    }

    /// <summary>Whether <paramref name="entry"/>'s backup is there and holds exactly the recorded original.</summary>
    /// <exception cref="IOException">The backup cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The backup cannot be opened.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public async Task<bool> IsBackupValidAsync(RecordEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return BackupOf(entry) is { } backup && await HoldsAsync(new FileInfo(backup), entry.Original!.Facts, cancellationToken);
    }

    private static async Task<bool> HoldsAsync(FileInfo file, FileFacts facts, CancellationToken cancellationToken) =>
        file.Exists
        && file.Length == facts.Size
        && string.Equals(await FileDigest.Sha256Async(file.FullName, cancellationToken), facts.Sha256, StringComparison.OrdinalIgnoreCase);

    // The backup's name is derived from the entry's path, never read from the record.
    private string? BackupOf(RecordEntry entry) =>
        _gameFolder is not null && entry.Original is { } original
            ? SafePath.Join(_gameFolder, entry.Path) + original.Suffix
            : null;

    // An entry moved aside holds nothing of ours under its path.
    private RecordEntry? Listed(string path) =>
        _gameFolder is not null && RecordPaths.KeyBelow(_gameFolder, path) is { } key && _entries.GetValueOrDefault(key) is { State: not EntryState.MovedAside } entry
            ? entry
            : null;
}
