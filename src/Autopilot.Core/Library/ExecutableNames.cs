using System.Text.RegularExpressions;

namespace Autopilot.Core.Library;

/// <summary>
/// What an executable's name says about it: a helper that is never the game, or a launcher that
/// starts the game as a new process. Both take the name without <c>.exe</c>.
/// </summary>
internal static partial class ExecutableNames
{
    // A name containing any of these (any case) is a helper. "dlss5-feed-host" is our own 32-bit
    // helper, which ranked above a 32-bit game it was taken for.
    private static readonly string[] _helperParts =
    [
        "srcds", "unins", "setup", "vcredist", "dxsetup", "dotnet", "prereq", "redist", "crashhandler",
        "crashreport", "crashpad", "easyanticheat", "battleye", "touchup", "installer", "activation", "cleanup",
        "helper", "webhelper", "unitycrashhandler", "ue4prereqsetup", "ue5prereqsetup", "epicwebhelper",
        "dlss5-feed-host", "digitalextras", "extrasapp", "artbook", "soundtrack",
    ];

    /// <summary>The negation of "looks like a helper".</summary>
    public static bool LooksLikeGame(string stem) =>
        !_helperParts.Any(part => stem.Contains(part, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A launcher: whatever is put in front of it is loaded by the launcher and unloaded when it
    /// hands over, so nothing draws.
    /// </summary>
    public static bool IsLauncherLike(string stem) => LauncherLike().IsMatch(stem);

    // "launcher" then a separator or the end (GTAVLauncher, UBOAT Launcher, not
    // SpaceLauncherSimulator); "launch" at the start likewise (Launch, Launch Game); Easy Anti-Cheat's
    // bootstrapper; a store's name then "launch" or "start".
    [GeneratedRegex(
        @"launcher([\s._-]|$)|^launch([\s._-]|$)|^start_protected_game$|^(eos|epic|ea|origin|uplay|ubisoft)[\s._-]?(launch|start)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex LauncherLike();
}
