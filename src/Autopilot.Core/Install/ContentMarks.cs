using System.Buffers;
using System.Text;
using Autopilot.Core.Graphics;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Install;

/// <summary>
/// Tells a component's DLL from a game's own file of the same name by what it carries: ReShade, DXVK and
/// REFramework each name themselves in their DLLs, which are over 1 MB, and OptiScaler's version resource names
/// its original file in UTF-16 whatever the file is called (what OptiScaler's own setup reads). With no install
/// record, this is how uninstall decides a proxy is one it may delete, and how an install tells what sits under
/// the name ReShade needs.
/// </summary>
internal static class ContentMarks
{
    /// <summary>ReShade's mark.</summary>
    public const string ReShade = "ReShade";

    /// <summary>DXVK's mark.</summary>
    public const string Dxvk = "DXVK";

    /// <summary>REFramework's mark.</summary>
    public const string Reframework = "REFramework";

    /// <summary>Special K's mark: an injector that can load ReShade, and names it.</summary>
    public const string SpecialK = "SpecialK";

    /// <summary>OptiScaler's original file name, which its version resource carries in UTF-16.</summary>
    public const string OptiScaler = KnownFiles.OptiScalerDll;

    private const long MinSize = 1 << 20;
    private const int ChunkSize = 1 << 20;

    // Every mark as the files carry it: the components' names in ASCII, OptiScaler's file name in UTF-16.
    private static readonly (string Mark, byte[] Bytes)[] _every =
    [
        (ReShade, Encoding.ASCII.GetBytes(ReShade)),
        (Dxvk, Encoding.ASCII.GetBytes(Dxvk)),
        (Reframework, Encoding.ASCII.GetBytes(Reframework)),
        (SpecialK, Encoding.ASCII.GetBytes(SpecialK)),
        (OptiScaler, Encoding.Unicode.GetBytes(OptiScaler)),
    ];

    /// <summary>Whether the file at <paramref name="path"/> is at least 1 MB and carries any of <paramref name="marks"/> (ASCII); false when it is not there or cannot be read.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task<bool> CarriesAnyAsync(string path, IReadOnlyList<string> marks, CancellationToken cancellationToken) =>
        Array.Exists(await FindAsync(path, [.. marks.Select(Encoding.ASCII.GetBytes)], enough: 1, cancellationToken), found => found);

    /// <summary>
    /// Every mark the file at <paramref name="path"/> carries (<see cref="ReShade"/>, <see cref="Dxvk"/>, <see cref="Reframework"/>,
    /// <see cref="SpecialK"/>, <see cref="OptiScaler"/>), read in one pass; none when it is under 1 MB, not there or cannot be read.
    /// OptiScaler and Special K DLLs may name ReShade too, so the caller decides which mark wins.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task<IReadOnlySet<string>> MarksAsync(string path, CancellationToken cancellationToken)
    {
        bool[] found = await FindAsync(path, [.. _every.Select(mark => mark.Bytes)], enough: _every.Length, cancellationToken);
        return _every.Where((_, at) => found[at]).Select(mark => mark.Mark).ToHashSet(StringComparer.Ordinal);
    }

    // Which of the marks the file carries; the scan stops once enough are found.
    private static async Task<bool[]> FindAsync(string path, byte[][] wanted, int enough, CancellationToken cancellationToken)
    {
        bool[] found = new bool[wanted.Length];
        try
        {
            await using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (file.Length >= MinSize)
            {
                await ScanAsync(file, wanted, found, enough, cancellationToken);
            }
        }
        catch (Exception unreadable) when (DrivePaths.IsNotThere(unreadable))
        {
            Array.Clear(found);
        }

        return found;
    }

    // One pass, every mark looked for in each chunk; chunks overlap by the longest mark's length less one, so a mark across two is found.
    private static async Task ScanAsync(FileStream file, byte[][] wanted, bool[] found, int enough, CancellationToken cancellationToken)
    {
        int overlap = wanted.Max(mark => mark.Length) - 1;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(ChunkSize + overlap);
        try
        {
            int kept = 0;
            int read;
            while ((read = await file.ReadAsync(buffer.AsMemory(kept, ChunkSize), cancellationToken)) > 0)
            {
                int filled = kept + read;
                for (int at = 0; at < wanted.Length; at++)
                {
                    found[at] |= buffer.AsSpan(0, filled).IndexOf(wanted[at]) >= 0;
                }

                if (found.Count(mark => mark) >= enough)
                {
                    return;
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
