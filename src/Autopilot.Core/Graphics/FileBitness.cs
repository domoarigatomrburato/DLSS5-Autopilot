using Autopilot.Core.Executables;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Graphics;

/// <summary>Reads files beside a game as executables, where a bad file is no evidence rather than an error.</summary>
internal static class FileBitness
{
    /// <summary>The file's bitness, or null when it is not an executable this tool can read.</summary>
    public static async Task<Bitness?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            using PeImage image = await PeImage.OpenAsync(path, cancellationToken);
            return image.Bitness;
        }
        catch (Exception e) when (DrivePaths.IsNotThere(e))
        {
            return null;
        }
    }

    /// <summary>The file's bitness and imports, or null when it cannot be read as an executable.</summary>
    public static async Task<ReadableExecutable?> ReadTablesAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return await Executable.ReadAsync(path, cancellationToken) as ReadableExecutable;
        }
        catch (PeException)
        {
            return null;
        }
    }
}
