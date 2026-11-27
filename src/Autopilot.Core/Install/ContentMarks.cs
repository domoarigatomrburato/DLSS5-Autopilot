using System.Buffers;
using System.Text;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Install;

/// <summary>
/// Tells a component's DLL from a game's own file of the same name by what it carries: ReShade, DXVK and
/// REFramework each name themselves in their DLLs, which are over 1 MB. With no install record, this is how
/// uninstall decides a proxy is one it may delete.
/// </summary>
internal static class ContentMarks
{
    /// <summary>ReShade's mark.</summary>
    public const string ReShade = "ReShade";

    /// <summary>DXVK's mark.</summary>
    public const string Dxvk = "DXVK";

    /// <summary>REFramework's mark.</summary>
    public const string Reframework = "REFramework";

    private const long MinSize = 1 << 20;
    private const int ChunkSize = 1 << 20;

    /// <summary>Whether the file at <paramref name="path"/> is at least 1 MB and carries any of <paramref name="marks"/> (ASCII); false when it is not there or cannot be read.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task<bool> CarriesAnyAsync(string path, IReadOnlyList<string> marks, CancellationToken cancellationToken)
    {
        byte[][] wanted = [.. marks.Select(Encoding.ASCII.GetBytes)];
        try
        {
            await using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return file.Length >= MinSize && await ScanAsync(file, wanted, cancellationToken);
        }
        catch (Exception unreadable) when (DrivePaths.IsNotThere(unreadable))
        {
            return false;
        }
    }

    // One pass, every mark looked for in each chunk; chunks overlap by the longest mark's length less one, so a mark across two is found.
    private static async Task<bool> ScanAsync(FileStream file, byte[][] wanted, CancellationToken cancellationToken)
    {
        int overlap = wanted.Max(mark => mark.Length) - 1;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(ChunkSize + overlap);
        try
        {
            int kept = 0;
            while (true)
            {
                int read = await file.ReadAsync(buffer.AsMemory(kept, ChunkSize), cancellationToken);
                if (read == 0)
                {
                    return false;
                }

                int filled = kept + read;
                if (wanted.Any(mark => buffer.AsSpan(0, filled).IndexOf(mark) >= 0))
                {
                    return true;
                }

                kept = Math.Min(overlap, filled);
                buffer.AsSpan(filled - kept, kept).CopyTo(buffer);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
