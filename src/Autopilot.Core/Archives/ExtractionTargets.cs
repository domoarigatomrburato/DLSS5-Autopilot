namespace Autopilot.Core.Archives;

/// <summary>Decides which file members are written, and where inside the <c>.part</c> destination.</summary>
internal interface IExtractionTarget
{
    /// <summary>The path to write <paramref name="memberPath"/> to, or null to skip it.</summary>
    string? Place(string memberPath);

    /// <summary>Called once every member was read; refuses a result that is incomplete.</summary>
    void Finish();
}

/// <summary>The one file member whose path ends with the given parts, ignoring case.</summary>
internal sealed class MemberTarget(string archivePath, string ending, string partFile) : IExtractionTarget
{
    private readonly string[] _ending = SafePath.Parts(ending) is { Length: > 0 } parts
        ? parts
        : throw new ArgumentException("The member ending is empty.", nameof(ending));

    private bool _found;

    public string? Place(string memberPath)
    {
        string[] parts = memberPath.Split('/');
        bool matches = parts.Length >= _ending.Length
            && parts.AsSpan()[^_ending.Length..].SequenceEqual(_ending, StringComparer.OrdinalIgnoreCase);
        if (!matches)
        {
            return null;
        }

        if (_found)
        {
            throw ArchiveException.For(
                ArchiveFailure.AmbiguousMember,
                archivePath,
                $"more than one member ends with '{ending}', the second is '{memberPath}'");
        }

        _found = true;
        return partFile;
    }

    public void Finish()
    {
        if (!_found)
        {
            throw ArchiveException.For(ArchiveFailure.MemberNotFound, archivePath, $"holds no member ending with '{ending}'");
        }
    }
}

/// <summary>The file members a <see cref="TreeOptions"/> selects, with their subfolders.</summary>
internal sealed class TreeTarget(string archivePath, string destination, string partFolder, TreeOptions options) : IExtractionTarget
{
    private readonly string[] _source = options.SourceFolder is null ? [] : SafePath.Parts(options.SourceFolder);
    private readonly HashSet<string> _placed = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly List<string> _written = [];

    /// <summary>The files written, in the caller's spelling of the destination.</summary>
    public IReadOnlyList<string> Written => _written;

    public string? Place(string memberPath)
    {
        string[]? parts = Relative(memberPath.Split('/'));
        if (parts is null || !Selected(parts[^1]))
        {
            return null;
        }

        string relative = string.Join('/', parts);
        if (!_placed.Add(relative))
        {
            throw ArchiveException.For(ArchiveFailure.DuplicateName, archivePath, $"two members land on '{relative}'");
        }

        _written.Add(SafePath.Join(destination, relative));
        return SafePath.Join(partFolder, relative);
    }

    public void Finish()
    {
    }

    private string[]? Relative(string[] parts)
    {
        if (options.StripTopFolder && parts.Length > 1)
        {
            parts = parts[1..];
        }

        bool under = parts.Length > _source.Length
            && parts.AsSpan()[.._source.Length].SequenceEqual(_source, StringComparer.OrdinalIgnoreCase);
        return under ? parts[_source.Length..] : null;
    }

    private bool Selected(string fileName) =>
        (options.Extensions is not { } extensions || extensions.Any(extension => fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
        && (options.Names is not { } names || names.Contains(fileName, StringComparer.OrdinalIgnoreCase));
}
