using Autopilot.Core.Components.GitHub;

namespace Autopilot.Core.Components.Mirror;

/// <summary>
/// The mirror's releases read from github.com's pages, walked only as far as needed: up to
/// <see cref="BuildsPerFamily"/> builds per family, every pinned build kept whatever the count, and a
/// pinned build the walk did not reach asked for by its tag.
/// </summary>
internal sealed class MirrorPages(ReleasePages pages, GitHubRepository repository)
{
    /// <summary>How many builds per family are read: enough to install with and to step back one build.</summary>
    public const int BuildsPerFamily = 3;

    /// <summary>The releases kept, each with its assets; <see langword="null"/> when none was found.</summary>
    public async Task<IReadOnlyList<GitHubRelease>?> ReadAsync(CancellationToken cancellationToken)
    {
        Walk walk = new();
        for (int page = 1; page <= ReleasePages.MaxPages && !walk.IsFull; page++)
        {
            string[] tags = [.. (await pages.TagsOnPageAsync(repository, page, cancellationToken)).Where(walk.IsNew)];
            if (tags.Length == 0)
            {
                break;
            }

            foreach (string tag in tags.Where(walk.Wants))
            {
                walk.Add(await pages.ReleaseAsync(repository, tag, cancellationToken));
            }
        }

        foreach (string tag in walk.PinsNotSeen())
        {
            walk.Add(await pages.ReleaseAsync(repository, tag, cancellationToken));
        }

        return walk.Releases.Count > 0 ? walk.Releases : null;
    }

    /// <summary>What the walk has seen and kept.</summary>
    private sealed class Walk
    {
        private readonly HashSet<string> _seen = [with(StringComparer.Ordinal)];
        private readonly Dictionary<BuildFamily, int> _kept = [];

        public List<GitHubRelease> Releases { get; } = [];

        public bool IsFull => MirrorFamilies.All.All(family => _kept.GetValueOrDefault(family) >= BuildsPerFamily);

        public string[] PinsNotSeen() => [.. RenodxPins.Tags.Where(tag => !_seen.Contains(tag))];

        public bool IsNew(string tag) => _seen.Add(tag);

        // A tag of a family with room left, or a pinned build.
        public bool Wants(string tag) =>
            MirrorFamilies.Classify(tag) is var (family, _)
            && (_kept.GetValueOrDefault(family) < BuildsPerFamily || RenodxPins.Tags.Contains(tag, StringComparer.Ordinal));

        // A release whose fragment answered and that carries its zip.
        public void Add(GitHubRelease? release)
        {
            if (release is not null && MirrorReleases.Entry(release) is { } entry)
            {
                Releases.Add(release);
                _kept[entry.Family] = _kept.GetValueOrDefault(entry.Family) + 1;
            }
        }
    }
}
