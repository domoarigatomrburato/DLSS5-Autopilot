namespace Autopilot.Core.Install.Plans;

/// <summary>A placed file's bytes: what they must hash to, and where they come from.</summary>
/// <param name="Facts">The hash and size.</param>
/// <param name="Source">Where the bytes are read from.</param>
internal sealed record PlacedBytes(FileFacts Facts, FileSource Source);

/// <summary>
/// Where a route plan takes the bytes of the files it places, by component id and file name: the gathered files in the
/// download cache, or, for the preview, names only; and what the gather says for the plan's header.
/// </summary>
internal abstract class PlanFiles
{
    private readonly Dictionary<string, string> _versions = [with(StringComparer.Ordinal)];
    private readonly List<string> _notes = [];
    private readonly List<string> _warnings = [];
    private readonly List<string> _skipped = [];

    /// <summary>Each component's id and the version gathered, for the record's header.</summary>
    public IReadOnlyDictionary<string, string> Versions => _versions;

    /// <summary>What the gather says, for the report.</summary>
    public IReadOnlyList<string> Notes => _notes;

    /// <summary>The warnings the gather gives.</summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>What the gather skipped.</summary>
    public IReadOnlyList<string> Skipped => _skipped;

    /// <summary>The bytes of <paramref name="file"/> of component <paramref name="id"/>.</summary>
    /// <exception cref="InvalidOperationException">The component was not gathered: the plan asks for what its route does not install.</exception>
    public abstract PlacedBytes Of(string id, string file);

    /// <summary>The content of a small file of component <paramref name="id"/> (a layer manifest); empty when only its name is known.</summary>
    public abstract ReadOnlyMemory<byte> Content(string id, string file);

    /// <summary>Why component <paramref name="id"/> could not be fetched, which the plan skips with a warning (ray reconstruction); null when it was.</summary>
    public abstract string? Failure(string id);

    internal void Version(string id, string label) => _versions[id] = label;

    /// <summary>Adds <paramref name="note"/> once; nothing when null.</summary>
    internal void Note(string? note)
    {
        if (note is not null && !_notes.Contains(note))
        {
            _notes.Add(note);
        }
    }

    /// <summary>Adds <paramref name="warning"/>; nothing when null.</summary>
    internal void Warn(string? warning)
    {
        if (warning is not null)
        {
            _warnings.Add(warning);
        }
    }

    internal void Skip(string skipped) => _skipped.Add(skipped);
}

/// <summary>
/// The preview's files: known by name only, so the preview stays offline and writes nothing (install.md, "The 'what will
/// happen?' preview"). Every file is <see cref="PendingSource"/>, which the transaction refuses to run.
/// </summary>
internal sealed class PreviewFiles : PlanFiles
{
    private static readonly PlacedBytes _pending = new(PendingSource.Facts, PendingSource.Instance);

    /// <summary>The one preview source.</summary>
    public static PreviewFiles Instance { get; } = new();

    public override PlacedBytes Of(string id, string file) => _pending;

    public override ReadOnlyMemory<byte> Content(string id, string file) => ReadOnlyMemory<byte>.Empty;

    public override string? Failure(string id) => null;
}

/// <summary>The gathered files: each in the download cache (or the person's own add-on where it is), hashed once.</summary>
internal sealed class GatheredFiles : PlanFiles
{
    private readonly Dictionary<(string Id, string File), Gathered> _files = [];
    private readonly Dictionary<string, string> _failed = [with(StringComparer.Ordinal)];

    public override PlacedBytes Of(string id, string file) =>
        _files.GetValueOrDefault((id, file)) is { } gathered
            ? new(gathered.Facts, new CachedSource(gathered.Path))
            : throw new InvalidOperationException($"{file} of '{id}' was not gathered");

    public override ReadOnlyMemory<byte> Content(string id, string file) =>
        _files.GetValueOrDefault((id, file))?.Content ?? throw new InvalidOperationException($"{file} of '{id}' was not gathered");

    public override string? Failure(string id) => _failed.GetValueOrDefault(id);

    /// <summary>Adds a file at <paramref name="path"/>, with its facts and, for a small file the plan reads, its content (null otherwise).</summary>
    internal void Add(string id, string file, string path, FileFacts facts, ReadOnlyMemory<byte>? content) =>
        _files[(id, file)] = new(path, facts, content ?? ReadOnlyMemory<byte>.Empty);

    internal void Fail(string id, string why) => _failed[id] = why;

    private sealed record Gathered(string Path, FileFacts Facts, ReadOnlyMemory<byte> Content);
}
