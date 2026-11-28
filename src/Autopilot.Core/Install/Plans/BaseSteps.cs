using Autopilot.Core.Components;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Graphics;
using Autopilot.Core.Resources;
using Autopilot.Core.Routes;

namespace Autopilot.Core.Install.Plans;

/// <summary>The steps of the ReShade base that follow what the game ships: its compiler, and its own DLSS runtimes.</summary>
internal static class BaseSteps
{
    /// <summary>install.md, "Moved-aside files": the game's compiler only when older than Windows', both versions said.</summary>
    public static IEnumerable<PlanStep> Compiler(PlanDraft draft)
    {
        FolderSurvey survey = draft.Survey;
        string path = survey.Beside(KnownFiles.Compiler);
        if (survey.At(path) is null)
        {
            return survey.MovedAside.Contains(path) ? [new MoveAside(path, BackupKind.Dlss5Off)] : [];
        }

        string game = survey.CompilerVersion ?? Texts.Text("PlanVersionUnknown");
        string windows = survey.SystemCompilerVersion ?? Texts.Text("PlanVersionUnknown");
        if (IsOlder(survey.CompilerVersion, survey.SystemCompilerVersion))
        {
            draft.Notes.Add(Texts.Format("PlanNoteCompilerAside", game, windows));
            return [new MoveAside(path, BackupKind.Dlss5Off)];
        }

        draft.Notes.Add(Texts.Format("PlanNoteCompilerKept", game, windows));
        return [];
    }

    /// <summary>
    /// <c>nvngx_dlss.dll</c> beside the executable, over the game's own (backed up), unless the route places none or the game's
    /// own beside it is kept; when the game's own are not kept, every copy it ships deeper (Unreal's plugins) is swapped too.
    /// </summary>
    public static IEnumerable<PlanStep> Dlss(PlanDraft draft)
    {
        RouteFacts facts = draft.Request.Facts;
        if (!RouteComponents.PlacesDlss(draft.Request.Route, facts))
        {
            draft.Skipped.Add(Texts.Text("PlanSkippedDlssRoute"));
            return [];
        }

        if (facts.DlssBesideExecutable && draft.Read.KeepGameDlss)
        {
            draft.Skipped.Add(Texts.Text("PlanSkippedDlssKept"));
            return [];
        }

        string beside = draft.Survey.Beside(KnownFiles.Dlss);
        IEnumerable<string> nested = draft.Read.KeepGameDlss ? [] : GameFiles(facts, KnownFiles.Dlss).Where(own => !SamePath(own, beside));
        return [.. nested.Prepend(beside).Select(path => draft.Place(path, ComponentIds.Dlss, KnownFiles.Dlss))];
    }

    /// <summary>
    /// <c>nvngx_dlssd.dll</c>: a swap, never an addition, over every copy the game ships; a download that failed is a skipped
    /// line and a warning, and a swap the previous install made is kept.
    /// </summary>
    public static IEnumerable<PlanStep> RayReconstruction(PlanDraft draft)
    {
        if (draft.Read.RayReconstructionLabel is null)
        {
            return [];
        }

        string beside = draft.Survey.Beside(KnownFiles.RayReconstruction);
        string[] own = [.. GameFiles(draft.Request.Facts, KnownFiles.RayReconstruction).OrderBy(path => !SamePath(path, beside))];
        if (own.Length == 0)
        {
            draft.Skipped.Add(Texts.Text("PlanSkippedDlssdAbsent"));
            return [];
        }

        return draft.Files.Failure(ComponentIds.Dlssd) is { } why
            ? Unswapped(draft, own, why)
            : [.. own.Select(path => draft.Place(path, ComponentIds.Dlssd, KnownFiles.RayReconstruction))];
    }

    // A ray-reconstruction download that failed: the swaps the previous install made stay, with its version; else the game keeps its own.
    private static KeepEntry[] Unswapped(PlanDraft draft, string[] own, string why)
    {
        KeepEntry[] kept = [.. own.Where(draft.Survey.Previous.Contains).Select(path => new KeepEntry(path))];
        if (kept.Length > 0 && draft.Survey.PreviousComponents.GetValueOrDefault(ComponentIds.Dlssd) is { } label)
        {
            draft.Components[ComponentIds.Dlssd] = label;
        }

        draft.Warnings.Add(Texts.Format(kept.Length > 0 ? "PlanWarningRuntimeFailedKept" : "PlanWarningRuntimeFailed", KnownFiles.RayReconstruction, why));
        return kept;
    }

    // Four-part file versions, each part compared as a number (10.0.17763.1 is older than 10.0.26100.1).
    private static bool IsOlder(string? game, string? windows) =>
        game is not null && windows is not null && new ComponentVersion(game).CompareTo(new(windows)) == VersionOrder.Older;

    // The game's own files named name, as the runtime reader found them (the facts are read with the record, so ours are left
    // out): relative to the game folder, '/' separated.
    private static string[] GameFiles(RouteFacts facts, string name) =>
        [.. facts.Runtimes.DlssEvidence.Select(RecordPaths.Key).Where(path => SamePath(Path.GetFileName(path), name))];

    private static bool SamePath(string one, string other) => string.Equals(one, other, StringComparison.OrdinalIgnoreCase);
}
