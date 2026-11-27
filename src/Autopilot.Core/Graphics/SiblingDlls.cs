using Autopilot.Core.Executables;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Graphics;

/// <summary>
/// The DLLs beside an executable that may name its renderer: up to 60 by name, not proxies, not
/// middleware that names every API, not ours, and loadable by the executable's process.
/// </summary>
internal static class SiblingDlls
{
    /// <summary>Sibling DLLs whose import tables are read at most, by name.</summary>
    private const int MaxSiblings = 60;

    /// <summary>The largest sibling DLLs whose strings are read.</summary>
    private const int MaxSiblingStrings = 6;

    private const long MinStringSibling = 512 * 1024;

    private const long MaxStringSibling = 64 * 1024 * 1024;

    /// <summary>Renderer names in the siblings' import tables, then in the strings of the largest, in the order seen.</summary>
    public static async Task<List<NameFinding>> FindAsync(GameFolder game, CancellationToken cancellationToken)
    {
        List<NameFinding> seen = [];
        List<(string Path, long Size)> loadable = [];
        foreach (string path in Candidates(game))
        {
            ReadableExecutable? tables = await FileBitness.ReadTablesAsync(path, cancellationToken);
            // A 32-bit process cannot load a 64-bit DLL, and an unreadable one says nothing.
            if (game.Bitness != Bitness.Bits32 || tables?.Bitness == Bitness.Bits32)
            {
                seen.AddRange(Imported(path, tables));
                loadable.Add((path, Size(path)));
            }
        }

        foreach ((string path, _) in loadable.Where(dll => dll.Size is >= MinStringSibling and <= MaxStringSibling).OrderByDescending(dll => dll.Size).Take(MaxSiblingStrings))
        {
            IReadOnlySet<string> names = await NameSearch.NamesInAsync(path, cancellationToken);
            if (NameFinding.Best(names, ApiReason.SiblingString, Texts.Format("ApiWhereSiblingString", Path.GetFileName(path))) is { } named)
            {
                seen.Add(named);
            }
        }

        return seen;
    }

    private static IEnumerable<NameFinding> Imported(string path, ReadableExecutable? tables)
    {
        string where = Texts.Format("ApiWhereSiblingImport", Path.GetFileName(path));
        return KnownFiles.Renderers
            .Where(renderer => tables?.Imports.Contains(renderer.Name) == true)
            .Select(renderer => new NameFinding(renderer.Name, renderer.Api, ApiReason.SiblingImport, where));
    }

    private static IEnumerable<string> Candidates(GameFolder game) =>
        game.Files
            .Where(file => file.Key.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                && !KnownFiles.NotTheGame.Contains(file.Key)
                && !KnownFiles.NamesEveryApi.Contains(file.Key)
                && !game.Written.Contains(file.Key))
            .OrderBy(file => file.Key, StringComparer.OrdinalIgnoreCase)
            .Take(MaxSiblings)
            .Select(file => file.Value);

    // A file gone since the listing has no size; Exists never throws, and Length then reads what it cached.
    private static long Size(string path)
    {
        FileInfo file = new(path);
        return file.Exists ? file.Length : 0;
    }
}
