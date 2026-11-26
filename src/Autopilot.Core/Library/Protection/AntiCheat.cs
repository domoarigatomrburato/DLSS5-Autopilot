using Autopilot.Core.Resources;

namespace Autopilot.Core.Library.Protection;

/// <summary>
/// The one anti-cheat detector: ReShade with add-ons is tampering to every kernel-level anti-cheat,
/// so install, update and the watcher ask here first. Detection is by file, not by a game list.
/// </summary>
public static class AntiCheat
{
    /// <summary>
    /// Looks for anti-cheat markers in the install folder and the game folder (both when they differ):
    /// every entry's name in the folder and, in each visible subfolder, the names of its first 80
    /// entries in name order. A folder that is missing or cannot be read gives
    /// <see cref="AntiCheatVerdict.Unknown"/>, which callers treat as an anti-cheat being present.
    /// The folders are listed on a thread-pool thread.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static Task<AntiCheatFinding> DetectAsync(string installFolder, string gameFolder, CancellationToken cancellationToken) =>
        Task.Run(() => AntiCheatScan.Run(installFolder, gameFolder, cancellationToken), cancellationToken);

    /// <summary>What to say before replacing a DLSS runtime the game itself ships.</summary>
    public static string SwapWarning(string fileName) => Texts.Format("SwapWarning", fileName);
}
