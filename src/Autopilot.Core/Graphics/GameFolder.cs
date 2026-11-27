using System.Collections.Frozen;
using Autopilot.Core.Executables;
using Autopilot.Core.Install;

namespace Autopilot.Core.Graphics;

/// <summary>
/// The executable as read and the files beside it: what every rule of the decision consults.
/// The folder is listed once; files are read only when a rule asks.
/// </summary>
internal sealed class GameFolder
{
    private GameFolder(string executable, ExecutableReading reading, ApiSearch search, FrozenDictionary<string, string> files, OwnedFiles owned)
    {
        Executable = executable;
        Folder = Path.GetDirectoryName(executable) ?? "";
        Search = search;
        Files = files;
        Written = Names(files, owned.IsListed);
        Ours = Names(files, owned.IsListedAsOurs);
        if (reading is ReadableExecutable readable)
        {
            Bitness = readable.Bitness;
            Imports = readable.Imports;
            DelayImports = readable.DelayImports;
        }
    }

    public string Executable { get; }

    public string Folder { get; }

    public ApiSearch Search { get; }

    /// <summary>Null for a protected executable, whose bitness is not known.</summary>
    public Bitness? Bitness { get; }

    public IReadOnlySet<string> Imports { get; } = FrozenSet<string>.Empty;

    public IReadOnlySet<string> DelayImports { get; } = FrozenSet<string>.Empty;

    /// <summary>The files directly in the executable's folder, by name ignoring case.</summary>
    public FrozenDictionary<string, string> Files { get; }

    /// <summary>The names beside the executable that the install record lists as written by this tool, over the game's own file or not.</summary>
    public FrozenSet<string> Written { get; }

    /// <summary>The written names that are not the game's: the record keeps no original of the game's under them.</summary>
    public FrozenSet<string> Ours { get; }

    /// <param name="executable">The game's executable.</param>
    /// <param name="reading">What was read from it.</param>
    /// <param name="search">Where else the decision may look.</param>
    /// <param name="record">The install record found for the game, beside the executable or elsewhere in its folder, or null: the files it lists beside the executable are this tool's.</param>
    /// <param name="cancellationToken">Cancels the listing.</param>
    public static GameFolder Open(string executable, ExecutableReading reading, ApiSearch search, RecordedInstall? record, CancellationToken cancellationToken)
    {
        string full = Path.GetFullPath(executable);
        string folder = Path.GetDirectoryName(full) ?? "";
        var files = FolderListing.Files(folder, cancellationToken)
            .ToFrozenDictionary(path => Path.GetFileName(path), path => path, StringComparer.OrdinalIgnoreCase);
        return new GameFolder(full, reading, search, files, new OwnedFiles(record));
    }

    private static FrozenSet<string> Names(FrozenDictionary<string, string> files, Func<string, bool> listed) =>
        files.Where(file => listed(file.Value)).Select(file => file.Key).ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>The path of a file beside the executable that the game shipped (not ours), or null.</summary>
    public string? GameFile(string name) => !Ours.Contains(name) && Files.TryGetValue(name, out string? path) ? path : null;

    /// <summary>
    /// Whether the executable's process could load this file: for a 32-bit executable only a
    /// 32-bit file is evidence, and an unreadable one is none; any other bitness keeps every file.
    /// </summary>
    public async Task<bool> FitsAsync(string path, CancellationToken cancellationToken) =>
        Bitness != Executables.Bitness.Bits32 || await FileBitness.ReadAsync(path, cancellationToken) == Executables.Bitness.Bits32;
}
