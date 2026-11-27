using Autopilot.Core.Components.Sources;
using Autopilot.Core.Graphics;
using Autopilot.Core.Platform;
using Autopilot.Core.Resources;
using Autopilot.Core.Routes;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install;

/// <summary>
/// Uninstall with no usable record: backups with the tool's suffixes put back, names unique to the tool's
/// components deleted, proxies deleted only when their content says they are ReShade, DXVK or REFramework, and
/// names games commonly ship left with a line saying so. With no record there is no hash to compare.
/// </summary>
/// <param name="disk">Makes the changes.</param>
/// <param name="logger">Where what was done is logged.</param>
internal sealed partial class KnownNamesCleanup(InstallDisk disk, ILogger logger)
{
    private readonly UninstallReport _report = new(logger);

    /// <summary>
    /// Cleans <paramref name="installFolder"/> up by known names, and puts back the backups of runtimes swapped
    /// elsewhere in <paramref name="gameFolder"/>. A damaged record (<paramref name="problem"/>) is first moved
    /// aside, kept as evidence.
    /// </summary>
    /// <exception cref="IOException">A change failed other than by a held file.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled before a change.</exception>
    public async Task<UninstallFinished> RunAsync(RecordProblem? problem, string gameFolder, string installFolder, CancellationToken cancellationToken)
    {
        installFolder = Path.GetFullPath(installFolder);
        LogNoRecord(logger, installFolder);
        if (problem is not null)
        {
            await KeepDamagedAsync(installFolder);
        }

        _report.Say(Texts.Text("UninstallNoRecord"));
        (List<string> backups, bool remix) = KnownNames.Scan(gameFolder, installFolder, cancellationToken);
        HashSet<string> restored = await RestoreAsync(gameFolder, backups);
        foreach (string full in KnownNames.PresentIn(installFolder, KnownNames.Ours).Where(full => !restored.Contains(full)))
        {
            await DeleteAsync(full, gameFolder);
        }

        await ByContentAsync(gameFolder, installFolder, restored, remix, cancellationToken);
        await LastAsync(installFolder, restored);
        return _report.Finished(outsideKept: false) with { WithoutRecord = true };
    }

    // The helper's own folder goes once the deletes left it empty, anything still in it keeping it; then the names
    // games ship themselves are said, left as they are.
    private async Task LastAsync(string installFolder, HashSet<string> restored)
    {
        await FolderCleanup.IfEmptyAsync(Path.Join(installFolder, KnownFiles.HelperFolder), disk);
        foreach (string full in KnownNames.PresentIn(installFolder, KnownNames.CommonlyShipped).Where(full => !restored.Contains(full)))
        {
            _report.Say(Texts.Format("UninstallLineNotTold", Path.GetFileName(full)));
        }
    }

    // The report names a file by its path in the game folder, as a record does.
    private static string Relative(string gameFolder, string full) => RecordPaths.KeyBelow(gameFolder, full) ?? Path.GetFileName(full);

    /// <summary>Moves the damaged record aside, out of the next install's way: kept as evidence, never deleted.</summary>
    private async Task KeepDamagedAsync(string installFolder)
    {
        string record = InstallRecords.PathOf(installFolder);
        string aside = InstallPaths.FirstFree(record, KnownFiles.CorruptSuffix);
        await disk.MoveAsync(record, aside, CancellationToken.None);
        LogDamagedKept(logger, installFolder, Path.GetFileName(aside));
        _report.Say(Texts.Format("UninstallDamagedKept", Path.GetFileName(aside)));
    }

    /// <summary>Puts each backup back over whatever is at its name, in one step; gives the names put back.</summary>
    private async Task<HashSet<string>> RestoreAsync(string gameFolder, IEnumerable<string> backups)
    {
        HashSet<string> restored = [with(StringComparer.OrdinalIgnoreCase)];
        foreach (string backup in backups)
        {
            string original = KnownNames.OriginalOf(backup);
            string path = Relative(gameFolder, original);
            if (await _report.TryAsync(path, () => File.Exists(original) ? disk.ReplaceAsync(backup, original) : disk.MoveAsync(backup, original, CancellationToken.None)))
            {
                _ = restored.Add(original);
                _report.Say(Texts.Format("UninstallLineRestored", path), counts: true);
            }
        }

        if (restored.Count > 0)
        {
            _report.Say(Texts.Text("UninstallRestoredNoRecord"));
        }

        return restored;
    }

    // A d3d9.dll beside a Remix runtime is the runtime's: left alone, whatever it carries.
    private async Task ByContentAsync(string gameFolder, string installFolder, HashSet<string> restored, bool remix, CancellationToken cancellationToken)
    {
        foreach ((string path, string[] marks) in KnownNames.ByContent)
        {
            string full = Path.GetFullPath(Path.Join(installFolder, path));
            if (restored.Contains(full) || !File.Exists(full))
            {
                continue;
            }

            if (remix && Path.GetFileName(full).Equals(KnownFiles.D3d9, StringComparison.OrdinalIgnoreCase))
            {
                _report.Say(Texts.Text("UninstallLineRemixRuntime"));
            }
            else if (await ContentMarks.CarriesAnyAsync(full, marks, cancellationToken))
            {
                await DeleteAsync(full, gameFolder);
            }
        }
    }

    private async Task DeleteAsync(string full, string gameFolder)
    {
        string path = Relative(gameFolder, full);
        if (await _report.TryAsync(path, () => disk.DeleteAsync(full, CancellationToken.None)))
        {
            _report.Say(Texts.Format("UninstallLineRemoved", path), counts: true);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No install record in {Folder}: cleaning up by known file names")]
    private static partial void LogNoRecord(ILogger logger, string folder);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The install record in {Folder} is damaged: kept as {Name}")]
    private static partial void LogDamagedKept(ILogger logger, string folder, string name);
}

/// <summary>The names uninstall goes by with no record: the tool's own, those it cannot tell from a game's, and its backups.</summary>
internal static class KnownNames
{
    /// <summary>The names the reference lists beside the components' own (<c>installer.py:5433-5460</c>): their configs and shaders.</summary>
    private static readonly string[] _alsoOurs =
    [
        "dlss5-bridge.cfg", "dlss5-feed.cfg", "reshade-shaders/Shaders/DLSS5_AIO_Feed.fx", "reshade-shaders/Shaders/vort_Motion.fx",
        "reshade-shaders/Textures/vort_BlueNoise.png",
    ];

    /// <summary>
    /// Names unique to the tool's components, relative to the install folder: our add-ons, the feeder's and
    /// RTX40MFG-Unlock's files where their sources list them, and the reference's others; never the names games
    /// ship themselves, nor dgVoodoo2's.
    /// </summary>
    public static readonly IReadOnlyList<string> Ours =
    [
        .. KnownFiles.OurAddons
            .Concat(FeederRelease.LooseNames.Select(FeederPath))
            .Concat(ListedComponents.MfgFiles)
            .Concat(ListedComponents.MfgFiles.Where(name => !name.EndsWith(".addon64", StringComparison.OrdinalIgnoreCase)).Select(name => "plugins/" + name))
            .Concat(_alsoOurs)
            .Distinct(StringComparer.OrdinalIgnoreCase),
    ];

    /// <summary>Names games and people commonly have themselves: never deleted by name alone. No route writes a marker that proves one ours yet.</summary>
    public static readonly IReadOnlyList<string> CommonlyShipped = [KnownFiles.Dlss, KnownFiles.FrameGeneration, KnownFiles.NeuralRendering, "ReShade.ini", "ReShadePreset.ini"];

    /// <summary>Names a component's DLL may sit under, and the marks that prove it the component's.</summary>
    public static readonly IReadOnlyList<(string Path, string[] Marks)> ByContent =
    [
        (KnownFiles.Dxgi, [ContentMarks.ReShade, ContentMarks.Dxvk]),
        (KnownFiles.D3d11, [ContentMarks.ReShade, ContentMarks.Dxvk]),
        (KnownFiles.D3d12, [ContentMarks.ReShade]),
        (KnownFiles.D3d10, [ContentMarks.ReShade]),
        (KnownFiles.D3d9, [ContentMarks.ReShade, ContentMarks.Dxvk]),
        (KnownFiles.OpenGL, [ContentMarks.ReShade]),
        (KnownFiles.D3d8, [ContentMarks.Dxvk]),
        (KnownFiles.Dinput8, [ContentMarks.Reframework]),
        ("bin/d3d9.dll", [ContentMarks.Dxvk]),
        ("bin/win64/d3d9.dll", [ContentMarks.Dxvk]),
    ];

    /// <summary>The suffixes of the tool's backups of a game's own file, both restored.</summary>
    private static readonly IReadOnlyList<string> _suffixes = [KnownFiles.BackupSuffix, KnownFiles.DlssOffSuffix];

    /// <summary>The swapped runtimes' backups, put back wherever in the game folder they are.</summary>
    private static readonly HashSet<string> _runtimeBackups = [with(StringComparer.OrdinalIgnoreCase), .. KnownFiles.NgxRuntimes.Select(name => name + KnownFiles.BackupSuffix)];

    // The feeder's shader goes with ReShade's shaders, its helper in its own folder, the rest beside the game.
    private static string FeederPath(string name) => Path.GetExtension(name).ToUpperInvariant() switch
    {
        ".FX" => "reshade-shaders/Shaders/" + name,
        ".EXE" => KnownFiles.HelperFolder + "/" + name,
        _ => name,
    };

    // The tool's backups in the install folder, and a swapped runtime's anywhere in the game folder, in one bounded walk as detection makes it.
    /// <summary>
    /// The tool's backups to put back, from one bounded walk of the game folder as detection makes it, and whether
    /// an RTX Remix runtime is installed there.
    /// </summary>
    public static (List<string> Backups, bool Remix) Scan(string gameFolder, string installFolder, CancellationToken cancellationToken)
    {
        SharedListings listings = new();
        List<string> backups = Backups(gameFolder, installFolder, listings, cancellationToken);
        return (backups, RemixRuntime.Find([installFolder, gameFolder], listings, cancellationToken) is not null);
    }

    /// <summary>The files of <paramref name="names"/> there are in <paramref name="installFolder"/>, full.</summary>
    public static IEnumerable<string> PresentIn(string installFolder, IEnumerable<string> names) =>
        names.Select(name => Path.GetFullPath(Path.Join(installFolder, name))).Where(File.Exists);

    private static List<string> Backups(string gameFolder, string installFolder, SharedListings listings, CancellationToken cancellationToken) =>
    [
        .. RuntimeWalk.Find(gameFolder, IsBackupName, [], listings, cancellationToken)
            .Select(hit => Path.GetFullPath(hit.Path))
            .Where(full => _runtimeBackups.Contains(Path.GetFileName(full)) || PathParts.IsAtOrBelow(installFolder, full))
            .Distinct(StringComparer.OrdinalIgnoreCase),
    ];

    private static bool IsBackupName(string name) =>
        _suffixes.Any(suffix => name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

    /// <summary>The game's file <paramref name="backup"/>, named by one of the tool's suffixes, keeps.</summary>
    public static string OriginalOf(string backup) =>
        backup[..^_suffixes.First(suffix => backup.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)).Length];
}
