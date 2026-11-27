using Autopilot.Core.Executables;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Components;

/// <summary>A file a component's downloads hold: its path inside them, with <c>/</c> between folders, and its full path in the cache.</summary>
internal sealed record HeldFile(string Relative, string Full);

/// <summary>
/// The shape check run before anything is placed: the files a release must hold, matched by the end
/// of their path (the MFG route's rule, applied to every component with a known file list), and the
/// add-ons' bitness.
/// </summary>
internal static class ComponentLayout
{
    /// <summary>Every file under an unpacked archive's folder.</summary>
    public static IEnumerable<HeldFile> Unpacked(string folder) =>
        Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Select(path => new HeldFile(Path.GetRelativePath(folder, path).Replace('\\', '/'), path));

    /// <summary>Each expected file, the one nearest the root when several match.</summary>
    /// <exception cref="ComponentException">A required file is missing: the release changed its layout.</exception>
    public static IReadOnlyDictionary<string, string> Match(ComponentRelease release, IReadOnlyList<HeldFile> held)
    {
        Dictionary<string, string> found = [with(StringComparer.OrdinalIgnoreCase)];
        List<string> missing = [];
        foreach (ExpectedFile expected in release.Files)
        {
            string? path = held
                .Where(file => Ends(file.Relative, expected.Path))
                .OrderBy(file => file.Relative.Length)
                .Select(file => file.Full)
                .FirstOrDefault();
            if (path is not null)
            {
                found[expected.Path] = path;
            }
            else if (!expected.Optional)
            {
                missing.Add(expected.Path);
            }
        }

        return missing.Count == 0
            ? found
            : throw ReleaseAssets.LayoutChanged(release.Id, release.Version.Label, string.Join(", ", missing));
    }

    /// <summary>Every <c>.addon64</c> must be a 64-bit Windows binary and every <c>.addon32</c> a 32-bit one.</summary>
    /// <exception cref="ComponentException">An add-on is not.</exception>
    public static async Task CheckAddonsAsync(ComponentRelease release, IReadOnlyDictionary<string, string> found, CancellationToken cancellationToken)
    {
        foreach ((string name, string path) in found)
        {
            Bitness? wanted = name.EndsWith(".addon64", StringComparison.OrdinalIgnoreCase) ? Bitness.Bits64
                : name.EndsWith(".addon32", StringComparison.OrdinalIgnoreCase) ? Bitness.Bits32
                : null;
            if (wanted is { } bitness && await BitnessOfAsync(path, cancellationToken) != bitness)
            {
                throw new ComponentException(
                    ComponentFailure.WrongBitness,
                    Texts.Format("ComponentAddonBitness", Path.GetFileName(name), ComponentCatalog.Of(release.Id).Source, release.Version.Label, bitness == Bitness.Bits64 ? 64 : 32));
            }
        }
    }

    private static bool Ends(string relative, string expected) =>
        string.Equals(relative, expected, StringComparison.OrdinalIgnoreCase)
        || relative.EndsWith("/" + expected, StringComparison.OrdinalIgnoreCase);

    private static async Task<Bitness?> BitnessOfAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            using PeImage image = await PeImage.OpenAsync(path, cancellationToken);
            return image.Bitness;
        }
        catch (PeException)
        {
            return null;
        }
    }
}
