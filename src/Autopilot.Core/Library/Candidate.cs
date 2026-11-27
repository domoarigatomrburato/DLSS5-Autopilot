using Autopilot.Core.Platform;

namespace Autopilot.Core.Library;

/// <summary>One executable found below a game folder, with the score it is ranked by.</summary>
internal sealed class Candidate
{
    private const int ExtensionLength = 4;

    private Candidate(string path, string stem, string relative, long length)
    {
        Path = path;
        Stem = stem;
        Relative = relative;
        int slash = relative.LastIndexOf('/', StringComparison.Ordinal);
        Directory = slash < 0 ? "" : relative[..slash];
        Length = length;
    }

    public string Path { get; }

    /// <summary>The file name without <c>.exe</c>, as written.</summary>
    public string Stem { get; }

    /// <summary>The path below the game folder, with <c>/</c> separators.</summary>
    public string Relative { get; }

    /// <summary>The folder part of <see cref="Relative"/>; empty directly in the game folder.</summary>
    public string Directory { get; }

    public long Length { get; }

    public bool InGameFolder => Directory.Length == 0;

    public double Score { get; set; }

    /// <summary>The candidate for a listed file, or null when the file vanished since it was listed.</summary>
    public static Candidate? Read(FileInfo file, string folder)
    {
        long length;
        try
        {
            length = file.Length;
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception e) when (DrivePaths.IsNotThere(e))
        {
            length = 0;
        }

        string relative = System.IO.Path.GetRelativePath(folder, file.FullName).Replace(System.IO.Path.DirectorySeparatorChar, '/');
        return new Candidate(file.FullName, file.Name[..^ExtensionLength], relative, length);
    }
}
