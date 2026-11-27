using Autopilot.Core.Graphics;
using Autopilot.Core.Storage;

namespace Autopilot.Core.Install;

/// <summary>
/// An install plan made concrete against the disk and the record, by <see cref="InstallResolver"/>: the
/// record the journal opens with, and per group the actions it takes. The transaction executes exactly
/// this; the preview reads it.
/// </summary>
/// <param name="Paths">The game folder's places.</param>
/// <param name="InstallFolder">The install folder, full: where the record is.</param>
/// <param name="Opening">
/// The record saved before the first change: state <see cref="RecordState.Installing"/>, the previous
/// install's entries carried over, and the previous record whole when there is one.
/// </param>
/// <param name="Groups">The groups, in order: the previous install's files set aside first, when any, then the plan's.</param>
internal sealed record ResolvedInstall(InstallPaths Paths, string InstallFolder, InstallRecord Opening, IReadOnlyList<ResolvedGroup> Groups)
{
    /// <summary>The steps of progress: the journal, one per group, the check and the commit.</summary>
    public int ProgressSteps => Groups.Count + 3;

    /// <summary>The entries this install journals, in order.</summary>
    public IEnumerable<RecordEntry> Journaled => Groups.SelectMany(group => group.Actions).SelectMany(action => action.Journal);

    /// <summary>
    /// Every change on disk the transaction makes when nothing fails, in order: the record's saves, each
    /// group's changes between the saves that journal them, and the commit, which deletes the held files.
    /// </summary>
    public IReadOnlyList<WriteStep> Steps()
    {
        WriteStep save = new(WriteKind.SaveRecord, InstallRecords.PathOf(InstallFolder));
        List<WriteStep> steps = [save];
        foreach (ResolvedGroup group in Groups.Where(group => group.Actions.Count > 0))
        {
            steps.Add(save);
            steps.AddRange(group.Actions.SelectMany(action => action.Changes(Paths)).SelectMany(change => change.Steps(IsReadOnlyWhenChanged)));
            steps.Add(save);
        }

        steps.Add(save);
        DeleteFile[] held = [.. Journaled.Where(HeldFiles.IsHeld).Select(entry => HeldFiles.DeleteOf(entry, Paths))];
        if (held.Length > 0)
        {
            steps.AddRange(held.SelectMany(change => change.Steps(IsReadOnlyWhenChanged)));
            steps.Add(save);
        }

        return steps;
    }

    // A held file is not there yet: the file it will hold is, with the attributes a rename keeps.
    private static bool IsReadOnlyWhenChanged(string path)
    {
        string now = !File.Exists(path) && path.EndsWith(KnownFiles.HeldSuffix, StringComparison.Ordinal) ? path[..^KnownFiles.HeldSuffix.Length] : path;
        return File.Exists(now) && File.GetAttributes(now).HasFlag(FileAttributes.ReadOnly);
    }
}

/// <summary>One group made concrete.</summary>
/// <param name="Name">The step's name, as progress shows it.</param>
/// <param name="Actions">The actions, in order; none when everything the group wants is already so.</param>
internal sealed record ResolvedGroup(string Name, IReadOnlyList<InstallAction> Actions);
