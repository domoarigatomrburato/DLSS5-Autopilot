using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Graphics;
using Autopilot.Core.Platform;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Install;

/// <summary>
/// What an install leaves that its record cannot name, taken out by uninstall after the entries: the helper
/// folder once empty, and the logs the components write while the game runs, by exact name only.
/// </summary>
/// <param name="record">The record being removed.</param>
/// <param name="paths">Its game folder.</param>
/// <param name="installFolder">The install folder, full: where the helper folder and the logs are.</param>
/// <param name="disk">Makes the changes.</param>
/// <param name="report">Says each log removed; one another program holds is only logged.</param>
internal sealed class RuntimeLeftovers(InstallRecord record, InstallPaths paths, string installFolder, InstallDisk disk, UninstallReport report)
{
    /// <summary>OptiScaler's own log folder, in the install folder: only its files are taken out of it.</summary>
    private const string OptiScalerLogs = "Logs";

    /// <summary>Written while the game runs, so the record cannot know them; removed by these exact names.</summary>
    private static readonly IReadOnlyList<string> _runtimeLogs =
    [
        "ReShade.log", "dlss5-feed.log", "dlss5-feed-host.log", "dlss5-bridge.log", "OptiScaler.log", "nvngx.log", "nvngx_dlssnr.log", "nvngx_dlss.log",
    ];

    /// <summary>DXVK names its logs after the executable and the DLL: <c>&lt;stem&gt;_d3d11.log</c> and so on.</summary>
    private static readonly IReadOnlyList<string> _dxvkLogModules = ["dxgi", "d3d11", "d3d9", "d3d8"];

    /// <summary>Removes the helper folder when the record named entries inside it and it is now empty.</summary>
    /// <exception cref="IOException">The folder could not be removed.</exception>
    public async Task HelperFolderAsync()
    {
        string helper = Path.Join(installFolder, KnownFiles.HelperFolder);
        bool recorded = record.Entries.Any(entry => PathParts.IsAtOrBelow(helper, paths.Full(entry.Path)));
        if (recorded)
        {
            await FolderCleanup.IfEmptyAsync(helper, disk);
        }
    }

    /// <summary>
    /// Removes the components' logs by exact name (DXVK's only when DXVK was ours), and OptiScaler's files from
    /// its log folder, the folder too when that empties it. A log another program holds is left.
    /// </summary>
    /// <exception cref="IOException">A log could not be removed other than by a held file.</exception>
    public async Task LogsAsync()
    {
        List<string> removed = [];
        IEnumerable<string> dxvk = record.Components.ContainsKey(ComponentIds.Dxvk)
            ? record.Game.Executables.SelectMany(executable => _dxvkLogModules.Select(module => $"{Path.GetFileNameWithoutExtension(executable)}_{module}.log"))
            : [];
        foreach (string name in _runtimeLogs.Concat(dxvk))
        {
            await LogFileAsync(Path.Join(installFolder, name), removed);
        }

        string logs = Path.Join(installFolder, OptiScalerLogs);
        int before = removed.Count;
        foreach (string file in DrivePaths.Files(logs).Where(file => Path.GetFileName(file).StartsWith("OptiScaler", StringComparison.OrdinalIgnoreCase)))
        {
            await LogFileAsync(file, removed);
        }

        if (removed.Count > before && !Directory.EnumerateFileSystemEntries(logs).Any())
        {
            await disk.DeleteFolderAsync(logs);
        }
    }

    private async Task LogFileAsync(string path, List<string> removed)
    {
        if (!File.Exists(path))
        {
            return;
        }

        string relative = paths.Relative(path);
        if (await report.TryAsync(relative, () => disk.DeleteAsync(path, CancellationToken.None), isItem: false))
        {
            removed.Add(relative);
            report.Say(Texts.Format("UninstallLineRemoved", relative), counts: true);
        }
    }
}
