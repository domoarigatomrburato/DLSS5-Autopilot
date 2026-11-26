using System.Collections.Frozen;
using Autopilot.Core.Executables;

namespace Autopilot.Core.Graphics;

/// <summary>
/// The executable as read and the files beside it: what every rule of the decision consults.
/// The folder is listed once; files are read only when a rule asks.
/// </summary>
internal sealed class GameFolder
{
    private GameFolder(string executable, ExecutableReading reading, ApiSearch search, FrozenDictionary<string, string> files, FrozenSet<string> written)
    {
        Executable = executable;
        Folder = Path.GetDirectoryName(executable) ?? "";
        Search = search;
        Files = files;
        Written = written;
        if (reading is ReadableExecutable readable)
        {
            Bitness = readable.Bitness;
            Imports = readable.Imports;
            DelayImports = readable.DelayImports;
        }

        // A file of ours whose original sits backed up beside it is the game's.
        Ours = written.Where(name => !files.ContainsKey(name + KnownFiles.BackupSuffix)).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
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

    /// <summary>Every name the install record lists as written by this tool.</summary>
    public FrozenSet<string> Written { get; }

    /// <summary>The written names that are not the game's: no backup of the game's original beside them.</summary>
    public FrozenSet<string> Ours { get; }

    public static async Task<GameFolder> OpenAsync(string executable, ExecutableReading reading, ApiSearch search, CancellationToken cancellationToken)
    {
        string full = Path.GetFullPath(executable);
        string folder = Path.GetDirectoryName(full) ?? "";
        var files = FolderListing.Files(folder, cancellationToken)
            .ToFrozenDictionary(path => Path.GetFileName(path), path => path, StringComparer.OrdinalIgnoreCase);
        FrozenSet<string> written = await InstallRecord.WrittenAsync(folder, cancellationToken);
        return new GameFolder(full, reading, search, files, written);
    }

    /// <summary>The path of a file beside the executable that the game shipped (not ours), or null.</summary>
    public string? GameFile(string name) => !Ours.Contains(name) && Files.TryGetValue(name, out string? path) ? path : null;

    /// <summary>
    /// Whether the executable's process could load this file: for a 32-bit executable only a
    /// 32-bit file is evidence, and an unreadable one is none; any other bitness keeps every file.
    /// </summary>
    public async Task<bool> FitsAsync(string path, CancellationToken cancellationToken) =>
        Bitness != Executables.Bitness.Bits32 || await FileBitness.ReadAsync(path, cancellationToken) == Executables.Bitness.Bits32;
}
