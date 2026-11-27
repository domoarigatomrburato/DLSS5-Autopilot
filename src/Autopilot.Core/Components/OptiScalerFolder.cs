using Autopilot.Core.Resources;

namespace Autopilot.Core.Components;

/// <summary>An unpacked OptiScaler package in the cache, read before anything is written to a game.</summary>
internal static class OptiScalerFolder
{
    private const string MainDll = "OptiScaler.dll";

    // The refusal's sample of names, programs first: they say what the archive is.
    private const int NamesShown = 6;

    private static readonly string[] _programEndings = [".exe", ".dll"];

    /// <summary>
    /// The folder of <paramref name="unpacked"/> that holds the package: itself, or the one folder
    /// it is wrapped in, so <c>OptiScaler.dll</c> is found where the proxy rename looks.
    /// </summary>
    /// <exception cref="OptiScalerException">No <c>OptiScaler.dll</c> at that root (a copy in a subfolder is not the one renamed to the proxy).</exception>
    public static string PackageRoot(string unpacked, string tag)
    {
        string root = Directory.GetFileSystemEntries(unpacked) is [var only] && Directory.Exists(only) ? only : unpacked;
        return Directory.EnumerateFiles(root).Any(path => string.Equals(Path.GetFileName(path), MainDll, StringComparison.OrdinalIgnoreCase))
            ? root
            : throw new OptiScalerException(OptiScalerFailure.NotOptiScaler, Texts.Format("OptiScalerNotAPackage", tag, Held(root)));
    }

    // A sample of what the package holds, programs first: four markdown files must not bury the one name that explains it.
    private static string Held(string root)
    {
        string[] names =
        [
            .. Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => Array.Exists(_programEndings, ending => name.EndsWith(ending, StringComparison.OrdinalIgnoreCase)) ? 0 : 1)
                .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Take(NamesShown),
        ];
        return names.Length > 0 ? string.Join(", ", names) : Texts.Text("OptiScalerHeldNothing");
    }
}
