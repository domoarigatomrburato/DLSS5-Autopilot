using Autopilot.Core.Graphics;
using Autopilot.Core.Library;
using Autopilot.Core.Library.Protection;
using Autopilot.Core.Resources;
using Autopilot.Core.Routes;

namespace Autopilot.Core.Install;

/// <summary>
/// What the person should know before an install, which does not stop it: the route's driver warnings and
/// reliability, a launcher-like executable, other DLSS hooks in the folder, an anti-cheat, an RE Engine game,
/// an Agility SDK request Windows cannot meet. Read-only.
/// </summary>
internal static class InstallWarnings
{
    /// <summary>The warnings for <paramref name="request"/>, in the order the preview shows them; the files <paramref name="prior"/> placed are never another tool's.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task<IReadOnlyList<string>> ForAsync(RefusalRequest request, PriorInstall prior, CancellationToken cancellationToken)
    {
        List<string> warnings = [.. OfRoute(request.Route), .. Launcher(request.Game), .. OtherHooks(request.InstallFolder, prior)];
        AntiCheatFinding antiCheat = await AntiCheat.DetectAsync(request.InstallFolder, request.GameFolder, cancellationToken);
        if (antiCheat.Verdict != AntiCheatVerdict.None)
        {
            warnings.Add(antiCheat.Explain());
        }

        if (request.Facts.IsReEngine && request.Route.Route != RouteId.Remix)
        {
            warnings.Add(ReEngine.Warning);
        }

        if (request.Agility is { } agility)
        {
            warnings.Add(agility.Explain());
        }

        return warnings;
    }

    // What the driver means for the route, and its reliability when it is not stable.
    private static IEnumerable<string> OfRoute(RouteOption route)
    {
        foreach (RouteMessage warning in route.Warnings)
        {
            yield return warning.Text();
        }

        if (route.Reliability != RouteReliability.Stable)
        {
            yield return Texts.Format(route.Reliability == RouteReliability.Beta ? "WarningBeta" : "WarningExperimental", route.Route.Name());
        }
    }

    // The executable looks like a launcher (GTAVLauncher.exe was all of issue #191): name the one that looks like the game.
    private static IEnumerable<string> Launcher(GameEntry game)
    {
        if (game.Executable is not { } executable || !ExecutableNames.IsLauncherLike(Path.GetFileNameWithoutExtension(executable)))
        {
            yield break;
        }

        string? real = game.Candidates.FirstOrDefault(candidate => !ExecutableNames.IsLauncherLike(Path.GetFileNameWithoutExtension(candidate)));
        yield return real is null
            ? Texts.Format("WarningLauncherAlone", Path.GetFileName(executable))
            : Texts.Format("WarningLauncher", Path.GetFileName(executable), Path.GetFileName(real));
    }

    // Another hook beside the executable (KnownFiles.IsAnotherHook), unless the previous install placed it.
    private static IEnumerable<string> OtherHooks(string installFolder, PriorInstall prior)
    {
        string[] hooks;
        try
        {
            hooks =
            [
                .. Directory.EnumerateFiles(installFolder)
                    .Where(path => KnownFiles.IsAnotherHook(Path.GetFileName(path)) && prior.At(path) is null)
                    .Select(Path.GetFileName)
                    .OfType<string>()
                    .Order(StringComparer.OrdinalIgnoreCase),
            ];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            hooks = [];
        }

        return hooks.Length > 0 ? [Texts.Format("WarningOtherHooks", string.Join(", ", hooks))] : [];
    }
}
