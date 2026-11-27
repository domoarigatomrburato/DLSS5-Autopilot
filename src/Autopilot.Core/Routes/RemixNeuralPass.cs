using System.Buffers;
using System.Text;
using Autopilot.Core.Graphics;

namespace Autopilot.Core.Routes;

/// <summary>
/// Whether a Remix runtime carries a neural pass, read from the option prefix its community fork embeds as plain
/// ASCII (remix.md, "Which fork"): <c>rtx.neuralRendering</c> (lunks' drop-in) or <c>rtx.neuralUplift</c>
/// (Kim2091's GTA IV fork). NVIDIA's own runtime has neither. The file (150 to 230 MB) is read in 4 MiB chunks that
/// overlap by the longest marker, never held whole, and no further than the first marker. A runtime that cannot be read
/// answers nothing: the install names the file rather than asking for the swap.
/// </summary>
internal static class RemixNeuralPass
{
    private const int ChunkBytes = 4 * 1024 * 1024;

    private static readonly byte[][] _markers = [Encoding.ASCII.GetBytes("rtx.neuralRendering"), Encoding.ASCII.GetBytes("rtx.neuralUplift")];

    /// <summary>The runtime's <c>d3d9.dll</c> in <paramref name="trex"/>.</summary>
    public static string RuntimeOf(string trex) => Path.Join(trex, KnownFiles.D3d9);

    /// <summary>Whether the runtime <c>d3d9.dll</c> in <paramref name="trex"/> carries a neural pass; null when it is missing or cannot be read, which says nothing either way.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task<bool?> HasAsync(string trex, CancellationToken cancellationToken)
    {
        try
        {
            await using FileStream file = new(RuntimeOf(trex), FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return await CarriesMarkerAsync(file, cancellationToken);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Reads chunk after chunk, each with the tail of the one before, until a marker shows or the file ends.
    private static async Task<bool> CarriesMarkerAsync(FileStream file, CancellationToken cancellationToken)
    {
        int overlap = _markers.Max(marker => marker.Length) - 1;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(ChunkBytes + overlap);
        try
        {
            int kept = 0;
            int read;
            while ((read = await file.ReadAsync(buffer.AsMemory(kept, ChunkBytes), cancellationToken)) > 0)
            {
                int length = kept + read;
                if (HoldsMarker(buffer.AsSpan(0, length)))
                {
                    return true;
                }

                kept = Math.Min(overlap, length);
                buffer.AsSpan(length - kept, kept).CopyTo(buffer);
            }

            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static bool HoldsMarker(ReadOnlySpan<byte> window)
    {
        foreach (byte[] marker in _markers)
        {
            if (window.IndexOf(marker) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
