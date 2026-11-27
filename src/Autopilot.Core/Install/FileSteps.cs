namespace Autopilot.Core.Install;

/// <summary>
/// A placed file, a file moved aside, a folder and a kept entry, resolved against the disk and the previous
/// install; it owns the entries the journal opens with, which only it adds to.
/// </summary>
/// <param name="paths">The game folder.</param>
/// <param name="prior">The previous install.</param>
/// <param name="backups">The backup decisions.</param>
internal sealed class FileSteps(InstallPaths paths, PriorInstall prior, BackupDecisions backups)
{
    private readonly List<RecordEntry> _carried = [];
    private readonly HashSet<string> _folders = [with(StringComparer.OrdinalIgnoreCase)];

    /// <summary>The previous install's entries the new record opens with: those kept, and the originals still aside.</summary>
    public IReadOnlyList<RecordEntry> Carried => _carried;

    public async Task<IReadOnlyList<InstallAction>> PlaceAsync(PlaceFile place, string full, CancellationToken cancellationToken) =>
        [.. FoldersOf(Path.GetDirectoryName(full)!), .. await WriteAsync(place, full, cancellationToken)];

    public async Task<IReadOnlyList<InstallAction>> AsideAsync(MoveAside aside, string full, CancellationToken cancellationToken)
    {
        string relative = paths.Relative(full);
        Prior? before = prior.At(full);
        FileFacts? current = await FileFacts.OfAsync(full, cancellationToken);
        if (current is null)
        {
            // Already out of the way: a backup the previous install kept behind the path stays as it is.
            KeepAside(relative, before);
            return [];
        }

        if (prior.IsOurs(before, full, current))
        {
            KeepAside(relative, before);
            return [HoldFile.Ours(before!.Entry)];
        }

        OriginalFile original = InstallPaths.Original(full, current, aside.Backup);
        return [.. await backups.ClearBackupNameAsync(full, before, aside.Backup, cancellationToken), new MoveAsideFile(relative, original)];
    }

    /// <summary>
    /// The folders a pattern's files need, and the pattern. The previous install's files it stands for join
    /// <paramref name="named"/>: they are the download's to replace, never set aside.
    /// </summary>
    public IReadOnlyList<InstallAction> Pattern(string full, HashSet<string> named)
    {
        named.UnionWith(prior.Entries.Select(entry => entry.Key).Where(previous => PlacePattern.Matches(full, previous)));
        return [.. FoldersOf(Path.GetDirectoryName(full)!), new WritePattern(paths.Relative(full))];
    }

    /// <summary>The previous install's entry at <paramref name="full"/>, kept as it is.</summary>
    public IReadOnlyList<InstallAction> Keep(string full)
    {
        _carried.Add(prior.At(full)!.Entry);
        return [];
    }

    /// <summary>The folder and its parents below the game folder that are not there yet, outermost first, each once.</summary>
    public List<InstallAction> FoldersOf(string full)
    {
        List<InstallAction> made = [];
        for (string? folder = full; folder is not null && paths.Relative(folder).Length > 0; folder = Path.GetDirectoryName(folder))
        {
            if (!Directory.Exists(folder) && _folders.Add(folder))
            {
                made.Insert(0, new MakeFolder(paths.Relative(folder)));
            }
        }

        return made;
    }

    // Over nothing, over our previous file, or over the game's file, which is backed up first.
    private async Task<IReadOnlyList<InstallAction>> WriteAsync(PlaceFile place, string full, CancellationToken cancellationToken)
    {
        Prior? before = prior.At(full);
        FileFacts? current = await FileFacts.OfAsync(full, cancellationToken);
        if (current is null)
        {
            return [new WriteNew(paths.Relative(full), place.Class, place.Facts, place.Source, before?.Entry.Original)];
        }

        return prior.IsOurs(before, full, current)
            ? [new ReplaceOurs(paths.Relative(full), place.Class, place.Facts, place.Source, before!.Entry)]
            : await backups.BackUpThenWriteAsync(place, full, InstallPaths.Original(full, current, BackupKind.Backup), before, cancellationToken);
    }

    private void KeepAside(string relative, Prior? before)
    {
        if (before?.Entry is { State: EntryState.MovedAside } aside)
        {
            _carried.Add(aside);
        }
        else if (before?.Entry.Original is { } original)
        {
            _carried.Add(new(relative, EntryClass.Component, EntryState.MovedAside, null, original));
        }
    }
}
