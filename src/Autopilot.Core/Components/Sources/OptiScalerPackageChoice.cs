using Autopilot.Core.Components.GitHub;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Components.Sources;

/// <summary>
/// Which archive of a release is the OptiScaler package to install. A release page can carry
/// other programs of its author's, and several variants of the package; a variant renamed upstream
/// walks past a name match, so the shape of the names decides as well. What the release carried
/// and what was chosen are logged, so an upstream rename reads as a log line.
/// </summary>
internal static partial class OptiScalerPackageChoice
{
    private const string PackageWord = "optiscaler";

    private static readonly string[] _archiveEndings = [".zip", ".7z"];

    /// <summary>The package, or <see langword="null"/> when the release carries none for <paramref name="build"/> (logged).</summary>
    public static GitHubAsset? Choose(GitHubRelease release, OptiScalerBuild build, ILogger logger)
    {
        GitHubAsset[] packages = [.. release.Assets.Where(IsPackage)];
        if (packages.Length == 0)
        {
            Log(logger, LogLevel.Information, release, LogNoPackage);
            return null;
        }

        GitHubAsset? chosen;
        if (build.VariantWord is { } variant)
        {
            chosen = Variant(packages, variant);
            if (chosen is null)
            {
                Log(logger, LogLevel.Information, release, (log, tag, names) => LogNoVariant(log, tag, names, variant));
                return null;
            }
        }
        else
        {
            chosen = Standard(release, packages, build.SkipWords, logger);
        }

        Log(logger, LogLevel.Information, release, (log, tag, names) => LogChosen(log, tag, names, chosen.Name));
        return chosen;
    }

    // An archive whose name says it is OptiScaler; anything else on the page is another program.
    private static bool IsPackage(GitHubAsset asset) =>
        Array.Exists(_archiveEndings, ending => asset.Name.EndsWith(ending, StringComparison.OrdinalIgnoreCase))
        && asset.Name.Contains(PackageWord, StringComparison.OrdinalIgnoreCase);

    // A build that exists for one variant takes that archive or nothing: never the standard one under its name.
    private static GitHubAsset? Variant(GitHubAsset[] packages, string variant) =>
        ByLength(packages.Where(asset => Carries(asset, variant))).FirstOrDefault();

    private static GitHubAsset Standard(GitHubRelease release, GitHubAsset[] packages, IReadOnlyList<string> skipWords, ILogger logger)
    {
        GitHubAsset[] kept = [.. packages.Where(asset => !skipWords.Any(word => Carries(asset, word)))];
        if (kept.Length == 0)
        {
            // Every archive looks like a variant: the names moved upstream, and the shape still answers.
            Log(logger, LogLevel.Warning, release, LogEveryVariant);
            kept = packages;
        }

        // A variant is the standard name with something added: the shortest name wins when it is the
        // stem of every other. Against an unrelated archive the release's own order stands.
        GitHubAsset[] ordered = [.. ByLength(kept)];
        string stem = Path.GetFileNameWithoutExtension(ordered[0].Name);
        return Array.TrueForAll(ordered, asset => asset.Name.StartsWith(stem, StringComparison.OrdinalIgnoreCase)) ? ordered[0] : kept[0];
    }

    private static bool Carries(GitHubAsset asset, string word) => asset.Name.Contains(word, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<GitHubAsset> ByLength(IEnumerable<GitHubAsset> assets) =>
        assets.OrderBy(asset => asset.Name.Length).ThenBy(asset => asset.Name, StringComparer.Ordinal);

    private static void Log(ILogger logger, LogLevel level, GitHubRelease release, Action<ILogger, string, string> write)
    {
        if (logger.IsEnabled(level))
        {
            write(logger, release.Tag, string.Join(", ", release.Assets.Select(asset => asset.Name)));
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "OptiScaler {Tag} carries {Names}: no OptiScaler package among them, passing it over")]
    private static partial void LogNoPackage(ILogger logger, string tag, string names);

    [LoggerMessage(Level = LogLevel.Information, Message = "OptiScaler {Tag} carries {Names}: none is the {Variant} package, passing it over")]
    private static partial void LogNoVariant(ILogger logger, string tag, string names, string variant);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OptiScaler {Tag} carries {Names}: every archive looks like a variant, so the names upstream have changed")]
    private static partial void LogEveryVariant(ILogger logger, string tag, string names);

    [LoggerMessage(Level = LogLevel.Information, Message = "OptiScaler {Tag} carries {Names}: installing {Chosen}")]
    private static partial void LogChosen(ILogger logger, string tag, string names, string chosen);
}
