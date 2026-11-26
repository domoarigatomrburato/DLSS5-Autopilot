namespace Autopilot.Core.Archives;

/// <summary>Which members of an archive a tree extraction writes, and where. Filters ignore case.</summary>
public sealed record TreeOptions
{
    /// <summary>Drops the first path part, the archive's root folder; a top-level file stays at the top.</summary>
    public bool StripTopFolder { get; init; }

    /// <summary>Only members under this folder, written relative to it. Applies after <see cref="StripTopFolder"/>.</summary>
    public string? SourceFolder { get; init; }

    /// <summary>Only file names ending with one of these, dot included (".dll").</summary>
    public IReadOnlyCollection<string>? Extensions { get; init; }

    /// <summary>Only these file names.</summary>
    public IReadOnlyCollection<string>? Names { get; init; }

    /// <summary>Every field as text, case folded and collections sorted, so options that select the same members read the same.</summary>
    internal string CanonicalKey() =>
        string.Join('\n', StripTopFolder ? "strip" : "keep", SourceFolder?.ToUpperInvariant() ?? "", Sorted(Extensions), Sorted(Names));

    private static string Sorted(IReadOnlyCollection<string>? values) =>
        values is null ? "*" : string.Join('\0', values.Select(value => value.ToUpperInvariant()).Order(StringComparer.Ordinal));
}
