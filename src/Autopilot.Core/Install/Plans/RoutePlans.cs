using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Graphics;
using Autopilot.Core.Resources;
using Autopilot.Core.Routes;

namespace Autopilot.Core.Install.Plans;

/// <summary>What a route plan is built for: the folders, the game, the route and the person's settings.</summary>
/// <param name="GameFolder">The game folder, full: the boundary of the plan.</param>
/// <param name="InstallFolder">The install folder (the executable's), full.</param>
/// <param name="Executable">The game's executable, relative to the game folder.</param>
/// <param name="Route">The chosen route.</param>
/// <param name="Facts">
/// The game and the card as the routes read them. Where the game keeps its own DLSS runtimes comes from its runtimes, which
/// must be read with the folder's record: the runtime reader leaves out the files the record lists as ours.
/// </param>
/// <param name="Settings">The person's settings; only those the route shows for the game are read.</param>
/// <param name="Game">The game as the record says it.</param>
/// <param name="ToolVersion">The build installing.</param>
internal sealed record PlanRequest(
    string GameFolder,
    string InstallFolder,
    string Executable,
    RouteId Route,
    RouteFacts Facts,
    RouteSettings Settings,
    RecordedGame Game,
    string ToolVersion);

/// <summary>
/// The <see cref="InstallPlan"/> of a route, as a function of the request, the folder as surveyed and the files source: the
/// one plan the preview prints and the transaction runs (routes.md, "What each route installs"). Groups come in a fixed
/// order: what is taken out of the way, the compiler, ReShade, its settings, the runtimes, the route's own files, the
/// graphics preference.
/// </summary>
internal static class RoutePlans
{
    /// <summary>The plan of <paramref name="request"/>'s route over the folder <paramref name="survey"/> read, placing <paramref name="files"/>' bytes.</summary>
    /// <exception cref="NotSupportedException">The route's plan is not written yet, or the game goes through DXVK (sub-task g2).</exception>
    public static InstallPlan For(PlanRequest request, FolderSurvey survey, PlanFiles files)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(survey);
        ArgumentNullException.ThrowIfNull(files);
        RouteSettings read = ShownSettings.Reset(request.Route, request.Facts, request.Settings);
        if (RouteComponents.UsesDxvk(request.Route, request.Facts, read.Dxvk))
        {
            throw new NotSupportedException($"the {request.Route.Id()} plan through DXVK is not written yet");
        }

        PlanDraft draft = new(request, read, survey, files);
        if (RouteComponents.IsReShadeRoute(request.Route))
        {
            ReShadeBase.Add(draft);
        }

        switch (request.Route)
        {
            case RouteId.Native:
                draft.Group("PlanStepRenodx", Native(draft));
                break;
            default:
                throw new NotSupportedException($"the {request.Route.Id()} plan is not written yet");
        }

        draft.Group("PlanStepPreference", Preference(request, read));
        return draft.Plan();
    }

    // routes.md, the native row: Krish's add-on beside the executable, or the person's own file in its place.
    private static IEnumerable<PlanStep> Native(PlanDraft draft) =>
        [draft.Place(draft.Survey.Beside(KnownFiles.RenodxAddon), ComponentIds.Renodx, KnownFiles.RenodxAddon)];

    private static IReadOnlyList<PlanStep> Preference(PlanRequest request, RouteSettings read) =>
        request.Facts.Gpu is { } gpu ? GraphicsPreferenceSteps.Of(gpu, read.UseNvidiaCard, request.Executable) : [];
}

/// <summary>A plan being built: its groups in order, and what its header will say.</summary>
internal sealed class PlanDraft(PlanRequest request, RouteSettings read, FolderSurvey survey, PlanFiles files)
{
    private readonly List<PlanGroup> _groups = [];

    public PlanRequest Request => request;

    /// <summary>The settings as the route reads them: those it hides back at their defaults.</summary>
    public RouteSettings Read => read;

    public FolderSurvey Survey => survey;

    public PlanFiles Files => files;

    public List<string> Notes { get; } = [];

    public List<string> Warnings { get; } = [];

    public List<string> Skipped { get; } = [];

    /// <summary>Versions the plan records over the gathered ones: a component it keeps from the previous install.</summary>
    public Dictionary<string, string> Components { get; } = [with(StringComparer.Ordinal)];

    /// <summary>A group of <paramref name="steps"/> named by the text <paramref name="name"/>; none when there are no steps.</summary>
    public void Group(string name, IEnumerable<PlanStep> steps)
    {
        PlanStep[] all = [.. steps];
        if (all.Length > 0)
        {
            _groups.Add(new(Texts.Text(name), all));
        }
    }

    /// <summary>A file of component <paramref name="id"/> placed at <paramref name="path"/>.</summary>
    public PlaceFile Place(string path, string id, string file)
    {
        PlacedBytes bytes = files.Of(id, file);
        return new(path, EntryClass.Component, bytes.Facts, bytes.Source);
    }

    // The gathered versions, with those the plan keeps from the previous install over them.
    private Dictionary<string, string> Versions()
    {
        Dictionary<string, string> versions = new(files.Versions, StringComparer.Ordinal);
        foreach ((string id, string label) in Components)
        {
            versions[id] = label;
        }

        return versions;
    }

    public InstallPlan Plan() => new(
        request.GameFolder,
        request.InstallFolder,
        new PlanHeader(
            request.ToolVersion,
            request.Game,
            request.Route,
            read,
            Versions(),
            [.. files.Notes, .. Notes],
            [.. files.Warnings, .. Warnings],
            [.. files.Skipped, .. Skipped]),
        _groups);
}
