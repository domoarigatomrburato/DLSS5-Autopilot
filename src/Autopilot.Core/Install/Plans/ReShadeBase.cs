using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;
using Autopilot.Core.Resources;
using Autopilot.Core.Routes;

namespace Autopilot.Core.Install.Plans;

/// <summary>
/// What every ReShade route installs before its own part (routes.md, "Every ReShade route"): ReShade under its proxy name
/// or as the Vulkan layer, every other ReShade and every other route's add-on out of its reach, the game's older compiler
/// aside, <c>ReShade.ini</c>, and the runtimes.
/// </summary>
internal static class ReShadeBase
{
    /// <summary>
    /// The name ReShade loads as on <paramref name="route"/>: the person's choice, else <c>d3d9.dll</c> for Direct3D 9,
    /// <c>opengl32.dll</c> for OpenGL, <c>dxgi.dll</c> otherwise. Null for a Vulkan game, or one going through DXVK: it gets
    /// the layer and no proxy.
    /// </summary>
    /// <param name="route">The route.</param>
    /// <param name="facts">The game.</param>
    /// <param name="read">The settings as the route reads them (<see cref="ShownSettings.Reset"/>).</param>
    public static string? ProxyName(RouteId route, RouteFacts facts, RouteSettings read)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(read);
        if (facts.Api == GraphicsApi.Vulkan || RouteComponents.UsesDxvk(route, facts, read.Dxvk))
        {
            return null;
        }

        return read.ReShadeLoadsAs?.FileName() ?? facts.Api switch
        {
            GraphicsApi.DX9 => KnownFiles.D3d9,
            GraphicsApi.OpenGL => KnownFiles.OpenGL,
            _ => KnownFiles.Dxgi,
        };
    }

    /// <summary>Adds the base's groups to <paramref name="draft"/>, in their order.</summary>
    public static void Add(PlanDraft draft)
    {
        string? proxy = ProxyName(draft.Request.Route, draft.Request.Facts, draft.Read);
        draft.Group("PlanStepCleanUp", CleanUp(draft, proxy));
        draft.Group("PlanStepCompiler", BaseSteps.Compiler(draft));
        draft.Group(proxy is null ? "PlanStepReShadeLayer" : "PlanStepReShade", [ReShade(draft, proxy)]);
        draft.Group("PlanStepReShadeIni", [ReShadeIni.Of(draft)]);
        draft.Group("PlanStepDlssnr", [draft.Place(draft.Survey.Beside(KnownFiles.NeuralRendering), ComponentIds.Dlssnr, KnownFiles.NeuralRendering)]);
        draft.Group("PlanStepDlss", BaseSteps.Dlss(draft));
        draft.Group("PlanStepDlssd", BaseSteps.RayReconstruction(draft));
    }

    // Every other ReShade out of its reach, the other routes' add-ons, the older bridge add-on, and what a hand-installed
    // OptiScaler under the proxy name brings. A file of ours is held by the transaction, since the plan no longer places it;
    // one not on the record is backed up; one the previous install keeps aside stays aside.
    private static IEnumerable<PlanStep> CleanUp(PlanDraft draft, string? proxy)
    {
        FolderSurvey survey = draft.Survey;
        IEnumerable<string> otherReShades = PlanNames.ReShadeProxies
            .Where(name => !string.Equals(name, proxy, StringComparison.OrdinalIgnoreCase))
            .Select(survey.Beside)
            .Where(path => survey.At(path) is null or { Content: ProxyContent.ReShade });
        List<string> aside = [.. otherReShades, .. PlanNames.PurgedOn(draft.Request.Route).Select(survey.Beside)];
        if (proxy is not null && survey.At(survey.Beside(proxy)) is { Content: ProxyContent.OptiScaler })
        {
            draft.Notes.Add(Texts.Format("PlanNoteOptiScalerMoved", proxy));
            aside.AddRange(PlanNames.OptiScalerCompanions.Select(survey.Beside));
        }

        return aside.Where(survey.KeptAside).Select(path => new MoveAside(path, BackupKind.Backup));
    }

    // ReShade for the game's architecture under the proxy name, or, for a Vulkan game, as its layer.
    private static PlanStep ReShade(PlanDraft draft, string? proxy)
    {
        RegisteredLayer layer = draft.Request.Facts.Bitness == Bitness.Bits32 ? RegisteredLayer.VulkanLayer32 : RegisteredLayer.VulkanLayer64;
        var kind = LayerKind.Of(layer);
        if (proxy is not null)
        {
            return draft.Place(draft.Survey.Beside(proxy), ComponentIds.ReShade, kind.Library);
        }

        PlacedBytes bytes = draft.Files.Of(ComponentIds.ReShade, kind.Library);
        return new RegisterLayer(layer, bytes.Facts, bytes.Source, draft.Files.Content(ComponentIds.ReShade, kind.Manifest));
    }
}
