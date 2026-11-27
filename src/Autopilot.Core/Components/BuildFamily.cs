namespace Autopilot.Core.Components;

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

/// <summary>Which mirror tags belong to which family.</summary>
internal static class MirrorFamilies
{
    // Longest prefix first: "dlssnr-" and "dlssg-" both start with "dlss", so the bare "dlss-" is tried last.
    private static readonly (string Prefix, BuildFamily Family)[] _prefixes =
    [
        ("renodx-dlss5", BuildFamily.Renodx),
        ("renodx-dlss-SF", BuildFamily.RenodxSf),
        ("dlssnr", BuildFamily.Dlssnr),
        ("dlssg", BuildFamily.Dlssg),
        ("dlss-", BuildFamily.Dlss),
    ];

    /// <summary>The family as a person knows it, for messages.</summary>
    public static string NameOf(BuildFamily family) => family switch
    {
        BuildFamily.Renodx => "renodx-dlss5",
        BuildFamily.RenodxSf => "renodx-dlss SF",
        BuildFamily.Dlssnr => "nvngx_dlssnr.dll",
        BuildFamily.Dlssg => "nvngx_dlssg.dll",
        BuildFamily.Dlss => "nvngx_dlss.dll",
        _ => "nvngx_dlssd.dll",
    };

    /// <summary>The families the mirror serves.</summary>
    public static IEnumerable<BuildFamily> All => _prefixes.Select(entry => entry.Family);

    /// <summary>The family of the first prefix the tag starts with, and its label: the tag minus the prefix and a leading <c>-</c>.</summary>
    public static (BuildFamily Family, string Label)? Classify(string tag)
    {
        foreach ((string prefix, BuildFamily family) in _prefixes)
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
        string prefix = _prefixes.Single(entry => entry.Family == family).Prefix;
        return prefix.EndsWith('-', StringComparison.Ordinal) ? prefix + label : prefix + "-" + label;
    }
}
