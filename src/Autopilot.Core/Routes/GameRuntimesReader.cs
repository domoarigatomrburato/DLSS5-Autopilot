using System.Collections.Frozen;
using Autopilot.Core.Graphics;
using Autopilot.Core.Install;

namespace Autopilot.Core.Routes;

/// <summary>
/// Reads what a game ships of its own (DLSS, FSR 2/3 or XeSS, an RTX Remix runtime) from file
/// names alone: no DLL is opened. Looks beside the executable and in the install folder, then in a
/// bounded walk of the game folder that skips the asset-only folders.
/// </summary>
public static class GameRuntimesReader
{
    // Any one of these means the game ships super-resolution DLSS.
    private static readonly FrozenSet<string> _superResolution = Set([.. KnownFiles.Streamline, .. KnownFiles.RenamedDlss, KnownFiles.Dlss]);

    private static readonly FrozenSet<string> _dlssNames = Set([.. _superResolution, KnownFiles.FrameGeneration, KnownFiles.RayReconstruction]);

    private static readonly FrozenSet<string> _frameGeneration = Set(KnownFiles.FrameGeneration, KnownFiles.StreamlineFrameGeneration);

    private static readonly FrozenSet<string> _fsr = Set(KnownFiles.FsrRuntimes);

    private static readonly FrozenSet<string> _upscalerNames = Set([.. KnownFiles.FsrRuntimes, .. KnownFiles.XessRuntimes]);

    private static readonly FrozenSet<string> _names = Set([.. _dlssNames, .. _upscalerNames]);

    /// <summary>What the game whose executable is at <paramref name="executablePath"/> ships of its own.</summary>
    /// <param name="executablePath">The game's executable.</param>
    /// <param name="gameFolder">The game's library folder, walked for runtimes nested under it.</param>
    /// <param name="installFolder">The folder this tool installs into; null for the executable's folder.</param>
    /// <param name="record">The install record found in <paramref name="installFolder"/>, or null: the files it lists are this tool's.</param>
    /// <param name="cancellationToken">Cancels the listings.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static Task<GameRuntimes> ReadAsync(string executablePath, string gameFolder, string? installFolder, RecordedInstall? record, CancellationToken cancellationToken) =>
        Task.Run(() => Read(executablePath, gameFolder, installFolder, record, cancellationToken), cancellationToken);

    private static GameRuntimes Read(string executablePath, string gameFolder, string? installFolder, RecordedInstall? record, CancellationToken cancellationToken)
    {
        string exeFolder = Path.GetDirectoryName(Path.GetFullPath(executablePath)) ?? "";
        string install = Path.GetFullPath(installFolder ?? exeFolder);
        string game = Path.GetFullPath(gameFolder);
        string[] beside = [.. new[] { exeFolder, install }.DistinctBy(RuntimeWalk.Normal, StringComparer.OrdinalIgnoreCase)];
        OwnedFiles owned = new(install, record?.Files ?? []);

        List<RuntimeHit> besideHits = [.. beside.SelectMany(folder => Beside(folder, cancellationToken)).Where(hit => owned.IsGames(hit.Path))];
        List<RuntimeHit> walkHits = [.. RuntimeWalk.Find(game, _names, beside, cancellationToken).Where(hit => owned.IsGames(hit.Path))];

        string[] dlss = Evidence(game, besideHits.Concat(walkHits).Where(hit => _dlssNames.Contains(hit.Name)));
        bool own = dlss.Any(path => _superResolution.Contains(Path.GetFileName(path)));
        string[] upscaler = own ? [] : UpscalerEvidence(game, besideHits, walkHits);
        string? remix = RemixRuntime.Find([exeFolder, install, game], cancellationToken);
        return new GameRuntimes(
            own,
            dlss,
            Kind(upscaler),
            upscaler,
            dlss.Any(path => string.Equals(Path.GetFileName(path), KnownFiles.RayReconstruction, StringComparison.OrdinalIgnoreCase)),
            dlss.Any(path => _frameGeneration.Contains(Path.GetFileName(path))),
            remix);
    }

    // Beside the executable first; the walk is asked only when nothing is there.
    private static string[] UpscalerEvidence(string game, List<RuntimeHit> besideHits, List<RuntimeHit> walkHits)
    {
        RuntimeHit[] near = [.. besideHits.Where(hit => _upscalerNames.Contains(hit.Name))];
        return Evidence(game, near.Length > 0 ? near : walkHits.Where(hit => _upscalerNames.Contains(hit.Name) && !hit.UnderUpscalerSkip));
    }

    private static Upscaler Kind(string[] evidence) =>
        evidence.Length == 0 ? Upscaler.None
        : evidence.Any(path => _fsr.Contains(Path.GetFileName(path))) ? Upscaler.Fsr
        : Upscaler.Xess;

    private static IEnumerable<RuntimeHit> Beside(string folder, CancellationToken cancellationToken) =>
        FolderListing.Files(folder, cancellationToken)
            .Where(path => _names.Contains(Path.GetFileName(path)))
            .Select(path => new RuntimeHit(path, UnderUpscalerSkip: false));

    private static string[] Evidence(string game, IEnumerable<RuntimeHit> hits) =>
        [.. hits.Select(hit => Path.GetRelativePath(game, hit.Path)).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];

    private static FrozenSet<string> Set(params IEnumerable<string> names) => names.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
