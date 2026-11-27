using Autopilot.Core.Resources;

namespace Autopilot.Core.Install;

/// <summary>A file the preview says the install writes.</summary>
/// <param name="Path">Relative to the game folder, <c>/</c> separated; for a pattern, its folder and its pattern.</param>
/// <param name="IsPattern">Whether it stands for files only a download reveals.</param>
public sealed record PreviewFile(string Path, bool IsPattern);

/// <summary>
/// "What will happen?": what an install would do to the folder as it is now, read from the same resolved plan
/// the transaction runs, never derived again. Seven lists, and the lines the window and the log show.
/// </summary>
/// <param name="Steps">The steps that change something, in order.</param>
/// <param name="Written">The files written, created or overwritten, in order.</param>
/// <param name="BackedUp">The files of the game's or the person's kept under a backup name, in order.</param>
/// <param name="CleanedUp">The files taken out of the way first: the previous install's, other routes' add-ons, the sidelined compiler.</param>
/// <param name="Outside">What the install changes outside the game folder, each with its scope and how it is undone.</param>
/// <param name="Blockers">Every reason the install would refuse.</param>
/// <param name="Warnings">What the person should know, which does not stop it.</param>
public sealed record InstallPreview(
    IReadOnlyList<string> Steps,
    IReadOnlyList<PreviewFile> Written,
    IReadOnlyList<string> BackedUp,
    IReadOnlyList<string> CleanedUp,
    IReadOnlyList<string> Outside,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings)
{
    private const int NamedWrites = 3;

    /// <summary>
    /// The preview's lines, in order: with a blocker, only <c>cannot install:</c> lines; otherwise the warnings, the
    /// clean-up, the writes (the first three named), the backups or that none is needed, and the outside effects
    /// or that there is none.
    /// </summary>
    public IReadOnlyList<string> Lines()
    {
        if (Blockers.Count > 0)
        {
            return [.. Blockers.Select(blocker => Texts.Format("PreviewCannotInstall", blocker))];
        }

        List<string> lines = [.. Warnings.Select(warning => Texts.Format("PreviewWarning", warning))];
        if (CleanedUp.Count > 0)
        {
            lines.Add(Texts.Format("PreviewCleanUp", string.Join(", ", CleanedUp)));
        }

        lines.Add(Texts.Format("PreviewWrite", Written.Count, Written.Count == 0 ? "" : Texts.Format("PreviewWriteFirst", FirstWrites())));
        lines.Add(BackedUp.Count > 0 ? Texts.Format("PreviewBackUp", string.Join(", ", BackedUp)) : Texts.Text("PreviewNoBackups"));
        lines.AddRange(Outside.Count > 0 ? Outside.Select(effect => Texts.Format("PreviewOutside", effect)) : [Texts.Text("PreviewNothingOutside")]);
        return lines;
    }

    private string FirstWrites()
    {
        IEnumerable<string> named = Written.Take(NamedWrites).Select(file => file.IsPattern ? Texts.Format("PreviewPattern", file.Path) : file.Path);
        return string.Join(", ", Written.Count > NamedWrites ? named.Append("...") : named);
    }

    /// <summary>The preview of the install <paramref name="check"/> resolved, under the blockers and warnings it found.</summary>
    internal static InstallPreview Of(InstallCheck check)
    {
        ResolvedInstall? resolved = check.Resolved;
        InstallAction[] actions = [.. resolved?.Actions ?? []];
        return new(
            [.. resolved?.Groups.Where(group => group.Actions.Count > 0).Select(group => group.Name) ?? []],
            [.. In(actions, PreviewLists.Written).Select(action => new PreviewFile(action.Path, action.Preview.HasFlag(PreviewLists.Pattern)))],
            [.. In(actions, PreviewLists.BackedUp).Select(action => action.Path)],
            [.. In(actions, PreviewLists.CleanedUp).Select(action => action.Path)],
            [.. actions.Select(action => action.Outside?.Text()).OfType<string>()],
            [.. check.Blockers.Select(blocker => blocker.Text)],
            [.. check.Warnings, .. resolved?.Opening.Warnings ?? []]);
    }

    private static IEnumerable<InstallAction> In(IEnumerable<InstallAction> actions, PreviewLists list) =>
        actions.Where(action => action.Preview.HasFlag(list));
}
