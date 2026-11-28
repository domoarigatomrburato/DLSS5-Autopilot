using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install;

/// <summary>
/// The install a transaction runs over (an update, a reinstall, a route switch) or a rollback returns to, as
/// the transaction's game folder names it: the one place its record is rebased, its placed entries indexed by
/// full path and its created folders listed, and which of its files and backups are still ours.
/// </summary>
internal sealed class PriorInstall
{
    private readonly OwnedFiles _owned;
    private readonly InstallPaths? _folder;
    private readonly InstallPaths _paths;
    private readonly Dictionary<string, Prior> _entries = [with(StringComparer.OrdinalIgnoreCase)];

    private PriorInstall(InstallRecord? record, string? folder, InstallPaths paths, ILogger logger)
    {
        _paths = paths;
        if (record is null || folder is null)
        {
            _owned = new(null);
            return;
        }

        Record = record;
        _folder = new(folder);
        _owned = new(record, folder);
        foreach (RecordEntry entry in record.Placed)
        {
            string full = _folder.Full(entry.Path);
            if (paths.Contains(full))
            {
                _entries[full] = new(entry with { Path = paths.Relative(full) }, entry);
            }
            else
            {
                InstallResolver.LogOutside(logger, entry.Path);
            }
        }

        CreatedFolders = new HashSet<string>(record.CreatedFolders.Select(_folder.Full), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The previous record, whole; null on a fresh install.</summary>
    public InstallRecord? Record { get; }

    /// <summary>Every placed entry inside the game folder, by full path.</summary>
    public IEnumerable<KeyValuePair<string, Prior>> Entries => _entries;

    /// <summary>The folders the previous install created, full.</summary>
    public IReadOnlySet<string> CreatedFolders { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The install a transaction over <paramref name="recorded"/> runs over: only an <see cref="InstallState.Installed"/> record is one.</summary>
    public static PriorInstall Of(RecordedInstall? recorded, InstallPaths paths, ILogger logger) =>
        recorded is { State: InstallState.Installed, Record: { } record, GameFolder: { } folder }
            ? new(record, folder, paths, logger)
            : new(null, null, paths, logger);

    /// <summary>The install the open <paramref name="record"/> in <paramref name="installFolder"/> was made over, which its rollback returns to.</summary>
    public static PriorInstall Before(InstallRecord record, string installFolder, InstallPaths paths, ILogger logger) =>
        record.Previous is { } previous && RecordPaths.GameFolderOf(installFolder, previous.InstallFolder) is { } folder
            ? new(previous, folder, paths, logger)
            : new(null, null, paths, logger);

    /// <summary>The entry naming <paramref name="full"/>; null when none does.</summary>
    public Prior? At(string full) => _entries.GetValueOrDefault(full);

    /// <summary>
    /// The record an install's journal opens with: state <see cref="RecordState.Installing"/>, <paramref name="header"/>'s
    /// parts, <paramref name="carried"/> as its first entries, and the previous install's config edits carried over (its registrations
    /// and graphics preferences only as the plan journals them again), with the previous record whole.
    /// </summary>
    public InstallRecord Opening(PlanHeader header, string installFolder, IReadOnlyList<RecordEntry> carried) => new(
        InstallRecords.Format,
        header.ToolVersion,
        RecordState.Installing,
        installFolder,
        header.Game,
        header.Route,
        header.Choices,
        header.Components,
        carried,
        Record?.ConfigEdits.Select(edit => edit with { File = Rebased(edit.File) }).ToList() ?? [],
        // Registrations and graphics preferences are the plan's to journal again; the rest are released at commit.
        [],
        [],
        [.. CreatedFolders.Select(_paths.Relative)],
        header.Notes,
        header.Warnings,
        header.Skipped)
    {
        Previous = Record,
    };

    /// <summary>Whether the file at <paramref name="full"/>, whose content is <paramref name="current"/>, is ours by <paramref name="prior"/>: written by the previous install and unchanged since.</summary>
    public bool IsOurs(Prior? prior, string full, FileFacts? current) =>
        prior is { Entry.State: not EntryState.MovedAside } && _owned.IsOurs(full, current);

    /// <summary>
    /// Whether the file at <paramref name="full"/> is ours by <paramref name="prior"/>, read only as far as needed: listed as written
    /// by the previous install, then its size, then its hash (<see cref="OwnedFiles.IsOursAsync"/>).
    /// </summary>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The file cannot be opened.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public async Task<bool> IsOursAsync(Prior? prior, string full, CancellationToken cancellationToken) =>
        prior is { Entry.State: not EntryState.MovedAside } && await _owned.IsOursAsync(full, cancellationToken);

    /// <summary>Whether <paramref name="prior"/> keeps an original whose backup holds exactly the recorded bytes.</summary>
    public async Task<bool> IsBackupValidAsync(Prior? prior, CancellationToken cancellationToken) =>
        prior is { Entry.Original: not null } && await _owned.IsBackupValidAsync(prior.Recorded, cancellationToken);

    private string Rebased(string relative) => _paths.Relative(_folder!.Full(relative));
}

/// <summary>One entry of the previous install: as the new install's game folder names it, and as its own record does.</summary>
/// <param name="Entry">Relative to the new install's game folder.</param>
/// <param name="Recorded">As recorded.</param>
internal sealed record Prior(RecordEntry Entry, RecordEntry Recorded);
