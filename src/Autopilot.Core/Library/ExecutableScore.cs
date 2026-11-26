namespace Autopilot.Core.Library;

/// <summary>
/// The base score of an executable: higher is more likely the real game. One method per row of
/// the table in the library spec, in its order; <see cref="ScoreAdjustments"/> holds what is done
/// to the scores afterwards.
/// </summary>
internal static class ExecutableScore
{
    private const double BytesPerMegabyte = 1024 * 1024;
    private const double MegabytesCounted = 300;

    public static void Rate(IReadOnlyList<Candidate> candidates, string folderName)
    {
        string folderKey = LettersAndDigits(folderName);
        foreach (Candidate candidate in candidates)
        {
            candidate.Score = Of(candidate, folderKey);
        }
    }

    private static double Of(Candidate exe, string folderKey) =>
        Shipping(exe)
        + BinariesFolder(exe.Relative)
        + EngineTooling(exe.Relative)
        + FolderName(exe, folderKey)
        + InGameFolder(exe)
        + Helper(exe)
        + Size(exe)
        + Depth(exe)
        + UnityData(exe);

    // Unreal's suffix.
    private static double Shipping(Candidate exe) => exe.Stem.Contains("shipping", StringComparison.OrdinalIgnoreCase) ? 1000 : 0;

    private static double BinariesFolder(string relative)
    {
        if (relative.Contains("/binaries/win64/", StringComparison.OrdinalIgnoreCase)
            || relative.Contains("/bin/win64/", StringComparison.OrdinalIgnoreCase)
            || relative.StartsWith("binaries/win64/", StringComparison.OrdinalIgnoreCase))
        {
            return 400;
        }

        if (relative.Contains("/binaries/win32/", StringComparison.OrdinalIgnoreCase) || relative.Contains("/bin/win32/", StringComparison.OrdinalIgnoreCase))
        {
            return 300;
        }

        return relative.Contains("/bin/", StringComparison.OrdinalIgnoreCase) || relative.StartsWith("bin/", StringComparison.OrdinalIgnoreCase) ? 200 : 0;
    }

    private static double EngineTooling(string relative) =>
        relative.Contains("/engine/binaries/", StringComparison.OrdinalIgnoreCase) || relative.StartsWith("engine/binaries/", StringComparison.OrdinalIgnoreCase) ? -900 : 0;

    // Letters and digits of both names, ignoring case, either containing the other, however short.
    private static double FolderName(Candidate exe, string folderKey)
    {
        string name = LettersAndDigits(exe.Stem);
        bool matches = name.Length > 0 && folderKey.Length > 0
            && (name.Contains(folderKey, StringComparison.OrdinalIgnoreCase) || folderKey.Contains(name, StringComparison.OrdinalIgnoreCase));
        return matches ? 350 : 0;
    }

    private static double InGameFolder(Candidate exe) => exe.InGameFolder ? 120 : 0;

    private static double Helper(Candidate exe) => ExecutableNames.LooksLikeGame(exe.Stem) ? 0 : -1500;

    // 1.2 a megabyte, at most 300 of them.
    private static double Size(Candidate exe) => Math.Min(exe.Length / BytesPerMegabyte, MegabytesCounted) * 1.2;

    private static double Depth(Candidate exe) => -15 * exe.Relative.Count(character => character == '/');

    // Unity keeps its data in "<name>_Data" beside a player that is under a megabyte.
    private static double UnityData(Candidate exe) =>
        Directory.Exists(Path.Join(Path.GetDirectoryName(exe.Path), exe.Stem + "_Data")) ? 300 : 0;

    private static string LettersAndDigits(string text) => string.Concat(text.Where(char.IsLetterOrDigit));
}
