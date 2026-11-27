using System.Collections.Frozen;

namespace Autopilot.Core.Library;

/// <summary>What a game added by hand is called.</summary>
internal static class GameNames
{
    private static readonly FrozenSet<string> _generic = new[]
    {
        "bin", "bin64", "binaries", "win64", "win32", "x64", "x86", "retail", "shipping", "release", "system", "exe", "executable",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The nearest folder name going up from the executable's folder that says something about the
    /// game: <c>D:\Games\World War Z\bin\WWZ.exe</c> is "World War Z" and
    /// <c>Ghostwire\Snowfall\Binaries\Win64\GWT.exe</c> is "Snowfall". When every folder is generic
    /// it is the executable's own folder; with no folder at all (<c>D:\Tetris.exe</c>), the executable's
    /// name without <c>.exe</c>. Both kinds of separator count, so a Windows path reads the same on
    /// every host.
    /// </summary>
    public static string DisplayNameFor(string executablePath)
    {
        string[] folders = FoldersAbove(executablePath);
        for (int i = folders.Length - 1; i >= 0; i--)
        {
            if (!IsGeneric(folders, i))
            {
                return folders[i];
            }
        }

        return folders.Length > 0 ? folders[^1] : Path.GetFileNameWithoutExtension(executablePath.Replace('\\', '/'));
    }

    // The folders from the root down to the executable's own, without a drive ("D:").
    private static string[] FoldersAbove(string executablePath)
    {
        string[] parts = executablePath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        int first = parts.Length > 0 && parts[0].EndsWith(':', StringComparison.Ordinal) ? 1 : 0;
        return parts[first..Math.Max(first, parts.Length - 1)];
    }

    // "Content" is a real game's own folder, except below an Xbox game folder, where it only holds
    // the game: X:\XboxGames\Forza\Content is "Forza".
    private static bool IsGeneric(string[] folders, int index) =>
        _generic.Contains(folders[index])
        || (folders[index].Equals("Content", StringComparison.OrdinalIgnoreCase)
            && folders.Take(Math.Max(0, index - 1)).Any(folder => folder.Equals("XboxGames", StringComparison.OrdinalIgnoreCase)));
}
