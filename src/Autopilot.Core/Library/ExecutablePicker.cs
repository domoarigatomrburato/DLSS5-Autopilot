using Autopilot.Core.Archives;
using Autopilot.Core.Install;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Library;

/// <summary>
/// Chooses a game's executable: the person's pick, else the store's pick unless it is a stub the
/// ranking sees past, else the best candidate; then the folder an earlier install wrote to.
/// </summary>
internal static class ExecutablePicker
{
    private const string Binaries = "Binaries";

    /// <param name="found">The game as its source lists it.</param>
    /// <param name="picked">The executable the person picked, when it still exists; it is never replaced.</param>
    /// <param name="records">Install's reader.</param>
    /// <param name="notes">Where the notes go.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    public static async Task<PickedExecutable> PickAsync(StoreGame found, string? picked, IInstallRecords records, List<LibraryNote> notes, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> ranked = await RankAsync(found.Folder, notes, cancellationToken);
        if (picked is not null)
        {
            return await PickedAsync(picked, OrOnly(ranked, picked), records, cancellationToken);
        }

        if ((StorePick(found) ?? (ranked.Count > 0 ? ranked[0] : null)) is not { } executable)
        {
            InstallState? install = (await records.FindAsync(found.Folder, cancellationToken))?.State;
            return new PickedExecutable(null, [], install is null ? null : found.Folder, install);
        }

        IReadOnlyList<string> candidates = OrOnly(ranked, executable);
        return await AdoptPreviousInstallAsync(found, PreferRanking(found, executable, candidates, notes), candidates, records, notes, cancellationToken);
    }

    /// <summary>An executable the person picked: files go beside it, whatever an earlier install recorded elsewhere.</summary>
    public static async Task<PickedExecutable> PickedAsync(string executable, IReadOnlyList<string> candidates, IInstallRecords records, CancellationToken cancellationToken)
    {
        RecordedInstall? record = await records.FindAsync(FolderOf(executable), cancellationToken);
        return new PickedExecutable(executable, candidates, null, record?.State);
    }

    private static async Task<IReadOnlyList<string>> RankAsync(string folder, List<LibraryNote> notes, CancellationToken cancellationToken)
    {
        CandidateSearch search = await ExecutableCandidates.RankAsync(folder, cancellationToken);
        if (search.StoppedEarly)
        {
            notes.Add(new SearchStoppedEarly(folder, search.FoldersSearched));
        }

        return search.Candidates;
    }

    // A store's pick counts while its file is there.
    private static string? StorePick(StoreGame found) => found.StoreExecutable is { } named && DrivePaths.IsFile(named) ? named : null;

    // Never an empty list when there is an executable: the one in use is then the only candidate.
    private static IReadOnlyList<string> OrOnly(IReadOnlyList<string> ranked, string executable) => ranked.Count > 0 ? ranked : [executable];

    // A store's launch executable is often a stub that starts the real one: Ghostwire Tokyo's GWT.exe
    // in the root starts Snowfall\Binaries\Win64\GWT.exe, and files beside the stub are never loaded.
    private static string PreferRanking(StoreGame found, string executable, IReadOnlyList<string> candidates, List<LibraryNote> notes)
    {
        string top = candidates[0];
        if (SamePath(top, executable) || !UnderBinaries(found.Folder, top) || UnderBinaries(found.Folder, executable))
        {
            return executable;
        }

        notes.Add(new StorePickReplaced(found.Name, Path.GetFileName(executable), Path.GetRelativePath(found.Folder, top)));
        return top;
    }

    // The ranking can list a game against another executable than the one an earlier install was
    // made for; the folder holding our record, and the executable it names, describe what was patched.
    private static async Task<PickedExecutable> AdoptPreviousInstallAsync(
        StoreGame found, string executable, IReadOnlyList<string> candidates, IInstallRecords records, List<LibraryNote> notes, CancellationToken cancellationToken)
    {
        string here = FolderOf(executable);
        foreach (string folder in RecordFolders(here, found.Folder, candidates))
        {
            if (await records.FindAsync(folder, cancellationToken) is not { } record)
            {
                continue;
            }

            bool beside = SamePath(folder, here);
            if (!beside)
            {
                notes.Add(new InstallRecordedElsewhere(found.Name, folder, Path.GetFileName(executable)));
            }

            string adopted = Named(folder, record) ?? (beside ? executable : candidates.FirstOrDefault(candidate => SamePath(FolderOf(candidate), folder)) ?? executable);
            return new PickedExecutable(adopted, candidates, SamePath(FolderOf(adopted), folder) ? null : folder, record.State);
        }

        return new PickedExecutable(executable, candidates, null, null);
    }

    // The executable's own folder, the game folder, then every candidate's, each once.
    private static IEnumerable<string> RecordFolders(string here, string gameFolder, IReadOnlyList<string> candidates) =>
        new[] { here, gameFolder }.Concat(candidates.Select(FolderOf)).DistinctBy(Normal, StringComparer.OrdinalIgnoreCase);

    // A name the record holds is a file name; anything that would leave the folder is not followed.
    private static string? Named(string folder, RecordedInstall record) =>
        record.Executables
            .Where(IsFileName)
            .Select(name => Path.Join(folder, name))
            .FirstOrDefault(DrivePaths.IsFile);

    private static bool IsFileName(string name)
    {
        try
        {
            SafePath.ValidateFileName(name);
            return true;
        }
        catch (UnsafePathException)
        {
            return false;
        }
    }

    // Below the game folder only: a library kept under a folder named Binaries says nothing.
    private static bool UnderBinaries(string gameFolder, string path) =>
        Path.GetRelativePath(gameFolder, path).Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries).Any(part => part.Equals(Binaries, StringComparison.OrdinalIgnoreCase));

    private static string FolderOf(string path) => Path.GetDirectoryName(Path.GetFullPath(path)) ?? path;

    private static string Normal(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool SamePath(string a, string b) => string.Equals(Normal(a), Normal(b), StringComparison.OrdinalIgnoreCase);
}
