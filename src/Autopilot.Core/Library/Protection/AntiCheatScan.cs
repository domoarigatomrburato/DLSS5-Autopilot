using Autopilot.Core.Platform;

namespace Autopilot.Core.Library.Protection;

/// <summary>
/// One search for anti-cheat markers in a game's folders: every entry's name in each folder, and the
/// names of the first 80 entries of each of its visible subfolders. Nothing is read but names.
/// </summary>
internal sealed class AntiCheatScan
{
    private const int SubfolderEntries = 80;
    private const int EvidenceLimit = 4;

    // Hidden and system entries count; an unreadable folder must throw, not come back empty.
    private static readonly EnumerationOptions _listing = new() { AttributesToSkip = 0, IgnoreInaccessible = false };

    private readonly List<string> _products = [];
    private readonly List<string> _evidence = [];
    private bool _unreadable;

    private AntiCheatScan()
    {
    }

    public static AntiCheatFinding Run(string installFolder, string gameFolder, CancellationToken cancellationToken)
    {
        AntiCheatScan scan = new();
        foreach (string folder in new[] { installFolder, gameFolder }.Select(Resolve).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            scan.Search(folder, cancellationToken);
        }

        // A marker found is an answer even when another folder could not be read.
        AntiCheatVerdict verdict = scan._products.Count > 0 ? AntiCheatVerdict.Present
            : scan._unreadable ? AntiCheatVerdict.Unknown
            : AntiCheatVerdict.None;
        return new AntiCheatFinding(
            verdict,
            [.. scan._products.Distinct().Order(StringComparer.Ordinal)],
            [.. scan._evidence.Distinct().Order(StringComparer.Ordinal).Take(EvidenceLimit)]);
    }

    // A path that cannot be resolved cannot be read either; the search then reports it unreadable.
    private static string Resolve(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        }
        catch (Exception e) when (DrivePaths.IsNotThere(e))
        {
            return folder;
        }
    }

    private void Search(string folder, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<FileSystemInfo>? entries = Read(() => new DirectoryInfo(folder).EnumerateFileSystemInfos("*", _listing).ToList());
        foreach (FileSystemInfo entry in entries ?? [])
        {
            Note(entry.Name);
            if (entry is DirectoryInfo directory && !IsHidden(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                SearchSubfolder(directory.FullName);
            }
        }
    }

    // The first 80 in name order (ordinal), so the same folder gives the same answer on every file system.
    private void SearchSubfolder(string folder)
    {
        List<string>? names = Read(() => Directory.EnumerateFileSystemEntries(folder, "*", _listing)
            .Select(path => Path.GetFileName(path))
            .Order(StringComparer.Ordinal)
            .Take(SubfolderEntries)
            .ToList());
        foreach (string name in names ?? [])
        {
            Note(name);
        }
    }

    // Null, and the search marked unreadable, when the folder cannot be listed (access denied, an I/O error,
    // gone, a path that is not one).
    private List<T>? Read<T>(Func<List<T>> list)
    {
        try
        {
            return list();
        }
        catch (Exception e) when (DrivePaths.IsNotThere(e))
        {
            _unreadable = true;
            return null;
        }
    }

    private static bool IsHidden(DirectoryInfo directory) =>
        directory.Name.StartsWith('.', StringComparison.Ordinal) || directory.Attributes.HasFlag(FileAttributes.Hidden);

    private void Note(string name)
    {
        if (AntiCheatMarkers.ProductOf(name) is { } product)
        {
            _products.Add(product);
            _evidence.Add(name);
        }
    }
}
