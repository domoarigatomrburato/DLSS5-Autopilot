using System.Buffers;
using System.Collections.Frozen;
using System.Text;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Graphics;

/// <summary>
/// Finds the renderer DLL names a file mentions in its bytes, as ANSI or UTF-16LE text, ignoring
/// case. A name counts only whole: <c>xd3d11.dll</c> and <c>libd3d11.dll</c> do not name <c>d3d11.dll</c>.
/// </summary>
internal static class NameSearch
{
    /// <summary>A file larger than this is not searched.</summary>
    private const long MaxFileSize = 512L * 1024 * 1024;

    private const int BlockSize = 4 * 1024 * 1024;

    // Longer than the longest needle (vulkan-1.dll in UTF-16, 24 bytes), so a name across a block edge is seen whole.
    private const int Overlap = 64;

    // Each byte widened to one char (Latin-1), so a UTF-16LE name reads as the name with a NUL after each letter.
    private static readonly (string Needle, string Name, int Width)[] _needles =
    [
        .. KnownFiles.Renderers.Select(renderer => (renderer.Name, renderer.Name, 1)),
        .. KnownFiles.Renderers.Select(renderer => (Encoding.Latin1.GetString(Encoding.Unicode.GetBytes(renderer.Name)), renderer.Name, 2)),
    ];

    private static readonly SearchValues<string> _search = SearchValues.Create([.. _needles.Select(needle => needle.Needle)], StringComparison.OrdinalIgnoreCase);

    /// <summary>The renderer names the file mentions; none for a file that is too large or cannot be read.</summary>
    public static async Task<FrozenSet<string>> NamesInAsync(string path, CancellationToken cancellationToken)
    {
        HashSet<string> found = [with(StringComparer.OrdinalIgnoreCase)];
        try
        {
            using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (file.Length <= MaxFileSize)
            {
                await SearchAsync(file, found, cancellationToken);
            }
        }
        catch (Exception e) when (DrivePaths.IsNotThere(e))
        {
            return [];
        }

        return found.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    private static async Task SearchAsync(FileStream file, HashSet<string> found, CancellationToken cancellationToken)
    {
        byte[] bytes = ArrayPool<byte>.Shared.Rent(BlockSize + Overlap);
        char[] chars = ArrayPool<char>.Shared.Rent(BlockSize + Overlap);
        try
        {
            int kept = 0;
            bool first = true;
            int read;
            while (found.Count < KnownFiles.Renderers.Count && (read = await file.ReadAtLeastAsync(bytes.AsMemory(kept, BlockSize), BlockSize, throwOnEndOfStream: false, cancellationToken)) > 0)
            {
                int length = kept + read;
                _ = Encoding.Latin1.GetChars(bytes, 0, length, chars, 0);
                Scan(chars.AsSpan(0, length), first ? 0 : 2, found);
                kept = Math.Min(Overlap, length);
                Array.Copy(bytes, length - kept, bytes, 0, kept);
                first = false;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
            ArrayPool<char>.Shared.Return(chars);
        }
    }

    // A match before `from` lies wholly in the overlap and was judged with its context in the block before.
    private static void Scan(ReadOnlySpan<char> text, int from, HashSet<string> found)
    {
        int at = from;
        while (at < text.Length)
        {
            int hit = text[at..].IndexOfAny(_search);
            if (hit < 0)
            {
                return;
            }

            at += hit;
            foreach ((string needle, string name, int width) in _needles)
            {
                if (text[at..].StartsWith(needle, StringComparison.OrdinalIgnoreCase) && StartsName(text, at, width))
                {
                    _ = found.Add(name);
                }
            }

            at++;
        }
    }

    // The character before a whole name is not part of a longer name.
    private static bool StartsName(ReadOnlySpan<char> text, int at, int width)
    {
        if (at < width)
        {
            return true;
        }

        char before = width == 1 ? text[at - 1] : (char)(text[at - 2] | (text[at - 1] << 8));
        return !(char.IsLetterOrDigit(before) || before is '_' or '-');
    }
}
