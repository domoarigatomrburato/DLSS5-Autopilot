using System.Collections.Frozen;
using Autopilot.Core.Graphics;

namespace Autopilot.Core.Routes;

/// <summary>A runtime file found, and whether it lies under a folder the upscaler search skips.</summary>
internal sealed record RuntimeHit(string Path, bool UnderUpscalerSkip)
{
    public string Name => System.IO.Path.GetFileName(Path);
}

/// <summary>
/// The bounded walk of a game folder for runtime DLLs by name: breadth first, children in name
/// order, links not followed, the asset-only folders skipped. Games keep DLSS deeper than beside the
/// executable (Unreal under <c>Engine\Plugins\...\ThirdParty\Win64</c>, CryEngine under <c>Bin\Win64Shared</c>).
/// </summary>
internal static class RuntimeWalk
{
    // Bounds, from the reference's walk: nine levels below the game folder and 6000 folders listed.
    // Only counts and the token end it; no clock does, so the same tree always gives the same answer.
    private const int MaxDepth = 9;
    private const int MaxFolders = 6000;

    /// <summary>
    /// Asset-only folders, which both game walks skip: they never hold DLLs and can hold hundreds of
    /// thousands of files.
    /// </summary>
    public static readonly IReadOnlyList<string> AssetFolders =
    [
        "content", "paks", "saved", "logs", "movies", "sounds", "music", "videos", "localization", "shadercache",
        "derivedcache", "cache", "textures", "maps", "levels", "audio", "data", "assets", "mods", "screenshots",
        "steamapps", "redist", "_commonredist",
    ];

    // The asset folders and this walk's own; any folder starting with '.' is skipped as well.
    private static readonly FrozenSet<string> _skipped = KnownFiles.Set([.. AssetFolders, "host64", "reshade-shaders"]);

    // A skipped folder is skipped for its size, not because no runtime can be under it: NBA 2K27 keeps
    // Streamline in data\streamline (#119). So a skipped folder is entered when one of its children
    // bears one of these names, and inside it only those children are followed.
    private static readonly FrozenSet<string> _runtimeHomes = KnownFiles.Set(
        "streamline", "dlss", "nvidia", "ngx", "nvngx", "binaries", "plugins", "win64", "x64", "bin");

    // OptiScaler's own package carries its bundled FSR and XeSS: never the game's upscaler.
    private static readonly FrozenSet<string> _upscalerSkipped = KnownFiles.Set("optiscaler", "licenses");

    private readonly record struct Pending(string Path, int Depth, bool Narrow, bool UnderUpscalerSkip);

    /// <summary>
    /// The files under <paramref name="root"/> named in <paramref name="names"/>, leaving out the files
    /// directly in <paramref name="listed"/> (already looked at), though their subfolders are walked.
    /// </summary>
    public static List<RuntimeHit> Find(string root, FrozenSet<string> names, IReadOnlyList<string> listed, SharedListings listings, CancellationToken cancellationToken)
    {
        FrozenSet<string> seen = KnownFiles.Set(listed.Select(Normal));
        List<RuntimeHit> hits = [];
        Queue<Pending> pending = new([new Pending(root, 0, false, false)]);
        for (int folders = 0; folders < MaxFolders && pending.Count > 0; folders++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Pending folder = pending.Dequeue();
            if (!seen.Contains(Normal(folder.Path)))
            {
                hits.AddRange(FolderListing.Files(folder.Path, cancellationToken)
                    .Where(path => names.Contains(Path.GetFileName(path)))
                    .Select(path => new RuntimeHit(path, folder.UnderUpscalerSkip)));
            }

            if (folder.Depth < MaxDepth)
            {
                foreach (Pending child in Children(folder, listings, cancellationToken))
                {
                    pending.Enqueue(child);
                }
            }
        }

        return hits;
    }

    /// <summary>One spelling of a folder, for comparing two paths to it.</summary>
    public static string Normal(string folder) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));

    private static IEnumerable<Pending> Children(Pending parent, SharedListings listings, CancellationToken cancellationToken)
    {
        foreach (string child in listings.RealFolders(parent.Path, cancellationToken))
        {
            string name = Path.GetFileName(child);
            bool skipped = _skipped.Contains(name);
            if (parent.Narrow ? !_runtimeHomes.Contains(name) : name.StartsWith('.', StringComparison.Ordinal) || (skipped && !HoldsRuntimeHome(child, listings, cancellationToken)))
            {
                continue;
            }

            yield return new Pending(child, parent.Depth + 1, skipped, parent.UnderUpscalerSkip || _upscalerSkipped.Contains(name));
        }
    }

    private static bool HoldsRuntimeHome(string folder, SharedListings listings, CancellationToken cancellationToken) =>
        listings.RealFolders(folder, cancellationToken).Any(child => _runtimeHomes.Contains(Path.GetFileName(child)));
}
