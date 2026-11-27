namespace Autopilot.Core.Archives;

/// <summary>
/// Admits every member of one archive, selected or not: its name passes the Windows rules, no two
/// paths are equal ignoring case, no path is both a file and a folder, and the entry count and declared sizes stay within the limits.
/// </summary>
internal sealed class MemberCatalog(string archivePath, ArchiveLimits limits, bool keepFiles)
{
    private readonly HashSet<string> _paths = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly FileTreeShape _shape = new();
    private readonly List<ArchiveMember> _files = [];
    private int _entries;

    /// <summary>The file members admitted so far, in archive order, when the catalog keeps them; empty otherwise.</summary>
    public IReadOnlyList<ArchiveMember> Files => _files;

    /// <summary>The sizes every member declared, added up.</summary>
    public long DeclaredTotal { get; private set; }

    /// <summary>Counts one entry against the limit.</summary>
    public void Count()
    {
        if (++_entries > limits.MaxEntries)
        {
            throw ArchiveException.For(ArchiveFailure.TooManyEntries, archivePath, $"holds more than {limits.MaxEntries} entries");
        }
    }

    /// <summary>
    /// Counts and checks a member, returning its path with '/' separators, or null for a folder entry
    /// that is the archive's root ("./").
    /// </summary>
    public string? Admit(string name, bool isDirectory, long size)
    {
        Count();
        string path = Normalise(name);
        if (path.Length == 0)
        {
            return isDirectory ? null : throw ArchiveException.For(ArchiveFailure.UnsafeName, archivePath, name, "holds a file with an empty name");
        }

        if (!_paths.Add(path))
        {
            throw ArchiveException.For(ArchiveFailure.DuplicateName, archivePath, path, $"holds '{path}' twice, ignoring case");
        }

        if (!(isDirectory ? _shape.AddFolder(path) : _shape.AddFile(path)))
        {
            throw ArchiveException.For(ArchiveFailure.DuplicateName, archivePath, path, $"holds '{path}' where a file and a folder meet, ignoring case");
        }

        DeclaredTotal += size;
        if (size < 0 || DeclaredTotal > limits.MaxTotalBytes)
        {
            throw ArchiveException.For(ArchiveFailure.TooLarge, archivePath, $"expands past {limits.MaxTotalBytes} bytes at '{path}'");
        }

        if (keepFiles && !isDirectory)
        {
            _files.Add(new(path, size));
        }

        return path;
    }

    /// <summary>Refuses an expansion over the ratio limit, once it is large enough to matter: of one member, or of the whole archive when <paramref name="member"/> is null.</summary>
    public void CheckRatio(long expanded, long compressed, string? member)
    {
        if (expanded > limits.RatioCheckedAbove && expanded > compressed * limits.MaxRatio)
        {
            throw ArchiveException.For(
                ArchiveFailure.RatioExceeded,
                archivePath,
                member,
                $"{(member is null ? "the archive" : $"member '{member}'")} expands from {compressed} to {expanded} bytes, more than {limits.MaxRatio} times");
        }
    }

    private string Normalise(string name)
    {
        try
        {
            return string.Join('/', SafePath.Parts(name));
        }
        catch (UnsafePathException unsafeName)
        {
            throw ArchiveException.For(ArchiveFailure.UnsafeName, archivePath, name, $"member {unsafeName.Message}", unsafeName);
        }
    }
}
