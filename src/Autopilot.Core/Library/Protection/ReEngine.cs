using Autopilot.Core.Resources;

namespace Autopilot.Core.Library.Protection;

/// <summary>Capcom's RE Engine, whose own tamper checks crash ReShade with add-on support.</summary>
public static class ReEngine
{
    private const string MarkerFile = "re_chunk_000.pak";

    private static readonly EnumerationOptions _anyCase = new()
    {
        MatchCasing = MatchCasing.CaseInsensitive,
        AttributesToSkip = 0,
        IgnoreInaccessible = true,
    };

    /// <summary>The warning to show for an RE Engine game.</summary>
    public static string Warning => Texts.Format("ReEngineWarning", MarkerFile);

    /// <summary>
    /// Whether the engine's marker file, <c>re_chunk_000.pak</c> in any case, is in the executable's
    /// folder or the game folder. A folder that cannot be read has no marker in it.
    /// </summary>
    public static bool IsReEngine(string executableFolder, string gameFolder) =>
        Holds(executableFolder) || Holds(gameFolder);

    private static bool Holds(string folder)
    {
        try
        {
            return Directory.EnumerateFiles(folder, MarkerFile, _anyCase).Any();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }
}
