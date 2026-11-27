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

    /// <summary><c>nvngx_dlssd.dll</c>: NVIDIA's own only; the mirror's tags are not read for it.</summary>
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
/// The families in one table: which mirror tags belong to each, its catalog row, and its name as a
/// person knows it (for NVIDIA's runtimes, the file's own name).
/// </summary>
internal static class MirrorFamilies
{
    // Longest prefix first: "dlssnr-" and "dlssg-" both start with "dlss", so the bare "dlss-" is tried last.
    // dlssd has no prefix: the mirror's tags are not read for it.
    private static readonly (BuildFamily Family, string? Prefix, string Id, string Name)[] _families =
    [
        (BuildFamily.Renodx, "renodx-dlss5", ComponentIds.Renodx, "renodx-dlss5"),
        (BuildFamily.RenodxSf, "renodx-dlss-SF", ComponentIds.RenodxSf, "renodx-dlss SF"),
        (BuildFamily.Dlssnr, "dlssnr", ComponentIds.Dlssnr, KnownFiles.NeuralRendering),
        (BuildFamily.Dlssg, "dlssg", ComponentIds.Dlssg, KnownFiles.FrameGeneration),
        (BuildFamily.Dlss, "dlss-", ComponentIds.Dlss, KnownFiles.Dlss),
        (BuildFamily.Dlssd, null, ComponentIds.Dlssd, KnownFiles.RayReconstruction),
    ];

    /// <summary>The families the mirror serves.</summary>
    public static IEnumerable<BuildFamily> All => Mirrored.Select(row => row.Family);

    private static IEnumerable<(BuildFamily Family, string Prefix)> Mirrored =>
        _families.Where(row => row.Prefix is not null).Select(row => (row.Family, row.Prefix!));

    /// <summary>The family as a person knows it, for messages; for NVIDIA's runtimes, the file's name.</summary>
    public static string NameOf(BuildFamily family) => Row(family).Name;

    /// <summary>The family's own catalog row.</summary>
    public static CatalogEntry EntryOf(BuildFamily family) => ComponentCatalog.Of(Row(family).Id);

    /// <summary>The family's own catalog row when the mirror publishes it; <see langword="null"/> for NVIDIA's runtimes.</summary>
    public static CatalogEntry? MirrorEntryOf(BuildFamily family) => EntryOf(family) is { Source: MirrorCatalog.Repository } entry ? entry : null;

    /// <summary>The family of the first prefix the tag starts with, and its label: the tag minus the prefix and a leading <c>-</c>.</summary>
    public static (BuildFamily Family, string Label)? Classify(string tag)
    {
        foreach ((BuildFamily family, string prefix) in Mirrored)
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
        string prefix = Mirrored.Single(row => row.Family == family).Prefix;
        return prefix.EndsWith('-', StringComparison.Ordinal) ? prefix + label : prefix + "-" + label;
    }

    private static (BuildFamily Family, string? Prefix, string Id, string Name) Row(BuildFamily family) =>
        Array.Find(_families, row => row.Family == family);
}
