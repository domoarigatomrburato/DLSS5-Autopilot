using Autopilot.Core.Routes;

namespace Autopilot.Core.Install.Plans;

/// <summary><c>ReShade.ini</c> as every ReShade route writes it: the keys the spec lists set in the person's own file.</summary>
internal static class ReShadeIni
{
    private const string AddonSection = "ADDON";
    private const string AddonSwitchSection = "RenoDX.DLSS5";
    private const string InputSection = "INPUT";

    /// <summary>
    /// routes.md: add-on loading; <c>renodx-dlss5</c>'s own switch only when absent (a deliberate off survives), and only on the
    /// routes that load that add-on besides renodx; the overlay key; every other line as the person left it. ShortFuse's
    /// renodx add-on keeps its own section, which its route writes.
    /// </summary>
    public static PlaceFile Of(PlanDraft draft)
    {
        IniText ini = IniText.Parse(draft.Survey.Content(draft.Survey.Beside(PlanNames.ReShadeIni))).Set(AddonSection, "AddonPath", @".\");
        if (RouteComponents.LoadsRenodx(draft.Request.Route) && draft.Request.Route != RouteId.Renodx)
        {
            _ = ini.SetIfAbsent(AddonSwitchSection, "NeuralUplift", "1");
        }

        if (draft.Read.OverlayKey is { } key)
        {
            _ = ini.Set(InputSection, "KeyOverlay", FormattableString.Invariant($"{key},0,0,0"));
        }

        byte[] bytes = ini.ToBytes();
        return new(draft.Survey.Beside(PlanNames.ReShadeIni), EntryClass.Config, FileFacts.Of(bytes), new GeneratedSource(bytes));
    }
}
