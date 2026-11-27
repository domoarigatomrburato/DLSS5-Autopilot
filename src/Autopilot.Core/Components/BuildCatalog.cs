namespace Autopilot.Core.Components;

/// <summary>
/// The builds each family offers, newest first, with NVIDIA's own runtimes in front of the mirror's
/// in <see cref="BuildFamily.Dlss"/>, <see cref="BuildFamily.Dlssd"/> and <see cref="BuildFamily.Dlssg"/>.
/// </summary>
public sealed class BuildCatalog
{
    private readonly IReadOnlyDictionary<BuildFamily, IReadOnlyList<BuildEntry>> _families;

    internal BuildCatalog(IReadOnlyDictionary<BuildFamily, IReadOnlyList<BuildEntry>> families, ReleaseSource source)
    {
        _families = families;
        Source = source;
    }

    /// <summary>Where the mirror's list came from; its notice is what the install says about it.</summary>
    public ReleaseSource Source { get; }

    /// <summary>The family's builds, newest first; empty when none could be read.</summary>
    public IReadOnlyList<BuildEntry> Entries(BuildFamily family) => _families.GetValueOrDefault(family) ?? [];

    /// <summary>
    /// The entry whose label or tag is <paramref name="wanted"/>, else the newest. Taking the newest
    /// in place of a wanted one is not said here: the result carries it for the caller to say.
    /// </summary>
    /// <exception cref="ComponentException">The family has no builds (<see cref="ComponentFailure.BuildListUnavailable"/>).</exception>
    public PickedBuild Pick(BuildFamily family, string? wanted)
    {
        IReadOnlyList<BuildEntry> entries = Entries(family);
        if (entries.Count == 0)
        {
            throw ComponentException.BuildListUnavailable(family);
        }

        if (string.IsNullOrEmpty(wanted))
        {
            return new(entries[0], WantedMissing: false);
        }

        BuildEntry? match = entries.FirstOrDefault(entry => entry.Label == wanted || entry.Tag == wanted);
        return match is not null ? new(match, WantedMissing: false) : new(entries[0], WantedMissing: true);
    }

    /// <summary>
    /// What placing a runtime tries, in order: the chosen entry, then the next one in its family. A
    /// first that fails (a blocked host answering an HTML page) is logged and the second is used.
    /// </summary>
    public IReadOnlyList<BuildEntry> Candidates(PickedBuild picked)
    {
        IReadOnlyList<BuildEntry> entries = Entries(picked.Entry.Family);
        int at = IndexOf(entries, picked.Entry);
        return at >= 0 && at + 1 < entries.Count ? [picked.Entry, entries[at + 1]] : [picked.Entry];
    }

    private static int IndexOf(IReadOnlyList<BuildEntry> entries, BuildEntry entry)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (ReferenceEquals(entries[i], entry))
            {
                return i;
            }
        }

        return -1;
    }
}
