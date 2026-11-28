using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Graphics;

namespace Autopilot.Core.Components.Mirror;

/// <summary>A list of interchangeable builds of one file, newest first: the mirror's, and NVIDIA's own runtimes.</summary>
public enum BuildFamily
{
    /// <summary><c>renodx-dlss5</c> (Krish), mirror tags <c>renodx-dlss5-&lt;v&gt;</c>.</summary>
    Renodx,

    /// <summary><c>renodx-dlss</c> "SF" (ShortFuse), mirror tags <c>renodx-dlss-SF-&lt;v&gt;</c>.</summary>
    RenodxSf,

    /// <summary><c>nvngx_dlssnr.dll</c>, mirror tags <c>dlssnr-&lt;label&gt;</c>, chosen per card.</summary>
    Dlssnr,

    /// <summary><c>nvngx_dlssg.dll</c>: NVIDIA's own first, then mirror tags <c>dlssg-&lt;v&gt;</c>.</summary>
    Dlssg,

    /// <summary><c>nvngx_dlss.dll</c>: NVIDIA's own first, then mirror tags <c>dlss-&lt;v&gt;</c>.</summary>
    Dlss,

    /// <summary><c>nvngx_dlssd.dll</c>: NVIDIA's own first, then mirror tags <c>dlssd-&lt;v&gt;</c>.</summary>
    Dlssd,
}

/// <summary>Who publishes a build.</summary>
public enum BuildPublisher
{
    /// <summary><c>RankFTW/rhi-repo</c>: a release's <c>.zip</c> holding the file.</summary>
    Mirror,

    /// <summary>NVIDIA's <c>NVIDIA/DLSS</c> SDK: the DLL itself, from the tree of a release tag.</summary>
    Nvidia,
}

/// <summary>
/// The families in one table: which mirror tags belong to each, its component id, and its name as a
/// person knows it (for NVIDIA's runtimes, the file's own name).
/// </summary>
internal static class MirrorFamilies
{
    private static readonly GitHubSource _mirror = GitHubSource.Parse(ComponentRepositories.Mirror);
    private static readonly GitHubSource _nvidia = GitHubSource.Parse(ComponentRepositories.Nvidia);

    // Longest prefix first: "dlssnr", "dlssd" and "dlssg" all start with "dlss", so the bare "dlss-" is tried last.
    private static readonly (BuildFamily Family, string Prefix, string Id, string Name)[] _families =
    [
        (BuildFamily.Renodx, "renodx-dlss5", ComponentIds.Renodx, "renodx-dlss5"),
        (BuildFamily.RenodxSf, "renodx-dlss-SF", ComponentIds.RenodxSf, "renodx-dlss SF"),
        (BuildFamily.Dlssnr, "dlssnr", ComponentIds.Dlssnr, KnownFiles.NeuralRendering),
        (BuildFamily.Dlssd, "dlssd", ComponentIds.Dlssd, KnownFiles.RayReconstruction),
        (BuildFamily.Dlssg, "dlssg", ComponentIds.Dlssg, KnownFiles.FrameGeneration),
        (BuildFamily.Dlss, "dlss-", ComponentIds.Dlss, KnownFiles.Dlss),
    ];

    /// <summary>The families the mirror serves: every one.</summary>
    public static IEnumerable<BuildFamily> All => _families.Select(row => row.Family);

    /// <summary>The family as a person knows it, for messages; for NVIDIA's runtimes, the file's name.</summary>
    public static string NameOf(BuildFamily family) => Row(family).Name;

    /// <summary>The component id the install record stores for the family's file.</summary>
    public static string IdOf(BuildFamily family) => Row(family).Id;

    /// <summary>The family's mirror row: where the mirror's zips come from and how they and the file inside are checked.</summary>
    public static CatalogEntry EntryOf(BuildFamily family) => ComponentCatalog.Of(Row(family).Id, _mirror);

    /// <summary>The catalog row of <paramref name="family"/>'s builds from <paramref name="publisher"/>: the mirror's, or NVIDIA's own.</summary>
    public static CatalogEntry EntryOf(BuildFamily family, BuildPublisher publisher) =>
        publisher == BuildPublisher.Mirror ? EntryOf(family) : ComponentCatalog.Of(Row(family).Id, _nvidia);

    /// <summary>The family of the first prefix the tag starts with, and its label: the tag minus the prefix and a leading <c>-</c>.</summary>
    public static (BuildFamily Family, string Label)? Classify(string tag)
    {
        foreach ((BuildFamily family, string prefix, _, _) in _families)
        {
            if (tag.StartsWith(prefix, StringComparison.Ordinal))
            {
                string label = tag[prefix.Length..].TrimStart('-');
                return (family, label.Length > 0 ? label : tag);
            }
        }

        return null;
    }

    /// <summary>The mirror's tag for a label of <paramref name="family"/> (<c>renodx-dlss5-4.55</c>).</summary>
    public static string TagOf(BuildFamily family, string label)
    {
        string prefix = Row(family).Prefix;
        return prefix.EndsWith('-', StringComparison.Ordinal) ? prefix + label : prefix + "-" + label;
    }

    private static (BuildFamily Family, string Prefix, string Id, string Name) Row(BuildFamily family) =>
        Array.Find(_families, row => row.Family == family);
}
