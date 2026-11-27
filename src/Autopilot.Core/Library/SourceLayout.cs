using Autopilot.Core.Platform;

namespace Autopilot.Core.Library;

/// <summary>
/// Valve's Source layout, by its two marks: <c>engine.dll</c> in <c>bin</c>, <c>bin\x64</c> or
/// <c>bin\win64</c>, and a <c>gameinfo.txt</c> in a folder beside <c>bin</c>. Both, because
/// <c>bin\engine.dll</c> alone is a name other engines use too.
/// </summary>
internal static class SourceLayout
{
    private static readonly string[] _engineFolders = ["", "x64", "win64"];

    public static bool IsIn(string folder)
    {
        if (!_engineFolders.Any(sub => File.Exists(Path.Join(folder, "bin", sub, "engine.dll"))))
        {
            return false;
        }

        try
        {
            return Directory.EnumerateDirectories(folder).Any(game => File.Exists(Path.Join(game, "gameinfo.txt")));
        }
        catch (Exception e) when (DrivePaths.IsNotThere(e))
        {
            return false;
        }
    }
}
