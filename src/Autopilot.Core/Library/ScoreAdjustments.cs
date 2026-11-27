using System.Text.RegularExpressions;

namespace Autopilot.Core.Library;

/// <summary>
/// What is done to the scores once every executable has one, a method per row of the library
/// spec's table of adjustments, in its order. They look at the other candidates, so none can run
/// before all are scored.
/// </summary>
internal static partial class ScoreAdjustments
{
    public static async Task ApplyAsync(string folder, IReadOnlyList<Candidate> candidates, CancellationToken cancellationToken)
    {
        TrialOrLauncherBesideTheFullGame(candidates);
        LauncherLikeBesideAnythingElse(candidates);
        SourceLayoutRoot(folder, candidates);
        await ThirtyTwoBitTwinAsync(candidates, cancellationToken);
    }

    // NeedForSpeedHeatTrial beside NeedForSpeedHeat, UBOAT Launcher beside UBOAT: -600 each. A
    // demo-only install keeps its executable.
    private static void TrialOrLauncherBesideTheFullGame(IReadOnlyList<Candidate> candidates)
    {
        HashSet<string> names = [with(StringComparer.OrdinalIgnoreCase), .. candidates.Select(candidate => Key(candidate.Directory, candidate.Stem))];
        foreach (Regex suffix in (Regex[])[TrialOrDemo(), LauncherSuffix()])
        {
            foreach (Candidate candidate in candidates)
            {
                string full = suffix.Replace(candidate.Stem, "");
                if (full.Length != candidate.Stem.Length && names.Contains(Key(candidate.Directory, full)))
                {
                    candidate.Score -= 600;
                }
            }
        }
    }

    // GTAVLauncher beside GTA5: -600. Only while something else could be the game, so a folder
    // holding nothing but a launcher still offers it.
    private static void LauncherLikeBesideAnythingElse(IReadOnlyList<Candidate> candidates)
    {
        if (candidates.All(candidate => ExecutableNames.IsLauncherLike(candidate.Stem)))
        {
            return;
        }

        foreach (Candidate candidate in candidates.Where(candidate => ExecutableNames.IsLauncherLike(candidate.Stem)))
        {
            candidate.Score -= 600;
        }
    }

    // Valve's Source layout: hl2.exe is 100 KB in the root while bin\ holds megabytes of tools.
    private static void SourceLayoutRoot(string folder, IReadOnlyList<Candidate> candidates)
    {
        List<Candidate> roots = [.. candidates.Where(candidate => candidate.InGameFolder
            && ExecutableNames.LooksLikeGame(candidate.Stem)
            && !ExecutableNames.IsLauncherLike(candidate.Stem))];
        if (roots.Count == 0 || !SourceLayout.IsIn(folder))
        {
            return;
        }

        foreach (Candidate candidate in roots)
        {
            candidate.Score += 500;
        }
    }

    // Subnautica.exe and Subnautica32.exe: the 32-bit one gives way, by 400 and not 600, so it stays
    // in the list for a person who plays that build.
    private static async Task ThirtyTwoBitTwinAsync(IReadOnlyList<Candidate> candidates, CancellationToken cancellationToken)
    {
        foreach (IGrouping<string, Candidate> twins in candidates
            .GroupBy(candidate => Key(candidate.Directory, BitnessMarker().Replace(candidate.Stem, "")), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1))
        {
            Dictionary<Candidate, Executables.Bitness> bits = [];
            foreach (Candidate twin in twins)
            {
                if (await Graphics.FileBitness.ReadAsync(twin.Path, cancellationToken) is { } bitness)
                {
                    bits[twin] = bitness;
                }
            }

            if (bits.ContainsValue(Executables.Bitness.Bits32) && bits.ContainsValue(Executables.Bitness.Bits64))
            {
                foreach (Candidate thirtyTwo in bits.Where(pair => pair.Value == Executables.Bitness.Bits32).Select(pair => pair.Key))
                {
                    thirtyTwo.Score -= 400;
                }
            }
        }
    }

    private static string Key(string directory, string stem) => directory + "/" + stem;

    [GeneratedRegex(@"[\s._-]*(trial|demo)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex TrialOrDemo();

    [GeneratedRegex(@"[\s._-]*launcher$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LauncherSuffix();

    // x86, x64, win32, win64, 32 or 64, then an optional "bit".
    [GeneratedRegex(@"[\s._-]*(x86|x64|win32|win64|32|64)(bit)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex BitnessMarker();
}
