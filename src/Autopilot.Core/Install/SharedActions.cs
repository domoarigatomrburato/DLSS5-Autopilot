using Autopilot.Core.Gpu;
using Autopilot.Core.Platform;
using Autopilot.Core.Resources;
using Autopilot.Core.Routes;

namespace Autopilot.Core.Install;

/// <summary>
/// A shared layer this install relies on: registered or refreshed in the layer folder, or a foreign one reused,
/// which changes nothing. Its files are no record entries: every install shares them.
/// </summary>
/// <param name="Path">The manifest relied on, full: ours in the layer folder, or the foreign one reused.</param>
/// <param name="Registration">What the record lists, before any change.</param>
/// <param name="Made">The changes: the layer files that differ, then the registry value when it is not already ours and enabled.</param>
internal sealed record LayerAction(string Path, LayerRegistration Registration, IReadOnlyList<DiskChange> Made) : InstallAction(Path)
{
    public override IEnumerable<RecordEntry> Journal => [];

    public override IEnumerable<LayerRegistration> Registrations => [Registration];

    public override PreviewLists Preview => PreviewLists.None;

    // Ours: what it loads into, for whom, where its files are and what takes it out; a reused one is never removed.
    public override RouteMessage? Outside
    {
        get
        {
            var kind = LayerKind.Of(Registration.Layer);
            string folder = System.IO.Path.GetDirectoryName(Registration.Manifest) ?? Registration.Manifest;
            return Registration.Reused ? new RouteMessage("OutsideLayerReused", kind.Shown, Registration.Manifest)
                : Registration.Layer == RegisteredLayer.OpenXr ? new RouteMessage("OutsideOpenXrLayer", folder)
                : new RouteMessage("OutsideVulkanLayer", kind.Name ?? "VK_LAYER_reshade", folder);
        }
    }

    public override IReadOnlyList<DiskChange> Changes(InstallPaths paths) => Made;
}

/// <summary>
/// A program's graphics preference set to "High performance", or our own value of the previous install kept with
/// its first record. Windows refusing the write never stops the install: the record drops the value and gains a warning.
/// </summary>
/// <param name="Path">The program, relative to the game folder.</param>
/// <param name="Edit">What the record lists, before the write.</param>
/// <param name="Writes">Whether the value is written; false when the previous install's value is kept.</param>
/// <param name="Other">The other GPU, which the program is kept off.</param>
/// <param name="Registry">The current user's registry, which holds the value.</param>
internal sealed record PreferenceAction(string Path, GraphicsPreferenceEdit Edit, bool Writes, GpuVendor Other, IUserRegistry Registry) : InstallAction(Path)
{
    public override PreviewLists Preview => PreviewLists.None;

    public override RouteMessage? Outside => new("OutsideGraphicsPreference", System.IO.Path.GetFileName(Edit.Program), GraphicsPreferences.Shown(Other));

    public override IEnumerable<RecordEntry> Journal => [];

    public override IEnumerable<GraphicsPreferenceEdit> Preferences => [Edit];

    public override IReadOnlyList<DiskChange> Changes(InstallPaths paths) =>
        Writes ? [new SetRegistryString(Registry, GraphicsPreferences.Key, Edit.Program, Edit.Written)] : [];

    public override InstallRecord? Tolerate(InstallRecord record, Exception failure)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (failure is not RegistryChangeException refused)
        {
            return null;
        }

        List<GraphicsPreferenceEdit> kept = [.. record.GraphicsPreferences];
        _ = kept.Remove(Edit);
        return record with
        {
            GraphicsPreferences = kept,
            Warnings = [.. record.Warnings, GraphicsPreferences.Refused(Edit.Program, refused.InnerException ?? refused)],
        };
    }
}

/// <summary>
/// Windows' per-program graphics preference: <c>HKCU\Software\Microsoft\DirectX\UserGpuPreferences</c>, a string
/// value named by the program's full path holding <c>Name=Value;</c> tokens, ours <c>GpuPreference=2;</c>. Every
/// other token is left as found.
/// </summary>
internal static class GraphicsPreferences
{
    /// <summary>The key, below <c>HKEY_CURRENT_USER</c>.</summary>
    public const string Key = @"Software\Microsoft\DirectX\UserGpuPreferences";

    /// <summary>"High performance": the NVIDIA card.</summary>
    public const string High = "2";

    private const string Token = "GpuPreference";

    /// <summary>The value of the last <c>GpuPreference</c> token in <paramref name="value"/>; null when it has none.</summary>
    public static string? Chosen(string? value) =>
        Segments(value).Select(Parse).LastOrDefault(token => IsOurs(token.Name)).Value;

    /// <summary><paramref name="value"/> with our token added at its end, every other byte as it was.</summary>
    public static string With(string? value) =>
        (value ?? "") + (value is { Length: > 0 } && !value.EndsWith(';', StringComparison.Ordinal) ? ";" : "") + $"{Token}={High};";

    /// <summary><paramref name="value"/> without any <c>GpuPreference</c> token; null when nothing else is left.</summary>
    public static string? Without(string value)
    {
        string[] kept = [.. Segments(value).Where(segment => !IsOurs(Parse(segment).Name))];
        return kept.All(string.IsNullOrWhiteSpace) ? null : string.Join(';', kept);
    }

    /// <summary>The other GPU's maker as a person reads it.</summary>
    public static string Shown(GpuVendor vendor) => vendor == GpuVendor.Amd ? "AMD" : vendor.ToString();

    /// <summary>The warning for a program whose preference Windows would not let the install read or write.</summary>
    public static string Refused(string program, Exception failure) =>
        Texts.Format("InstallPreferenceRefused", Path.GetFileName(program), failure.Message);

    /// <summary>The value name under <see cref="Key"/> that names <paramref name="program"/> as a resolved path; null when none does.</summary>
    /// <exception cref="IOException">The key cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The key cannot be opened.</exception>
    /// <exception cref="System.Security.SecurityException">The key cannot be opened.</exception>
    public static string? NameOf(IUserRegistry user, string program) =>
        SharedPaths.Naming(user.ValueNames(Key), program).FirstOrDefault();

    private static bool IsOurs(string name) => string.Equals(name.Trim(), Token, StringComparison.OrdinalIgnoreCase);

    // Split on ';' keeping every segment's text, the empty one after a final ';' included.
    private static string[] Segments(string? value) => value is null ? [] : value.Split(';');

    private static (string Name, string? Value) Parse(string segment)
    {
        int equals = segment.IndexOf('=', StringComparison.Ordinal);
        return equals < 0 ? (segment, null) : (segment[..equals], segment[(equals + 1)..].Trim());
    }
}

/// <summary>Paths outside the game folder compared as the platform's game identity compares folders: resolved, ignoring case.</summary>
internal static class SharedPaths
{
    /// <summary>Whether <paramref name="first"/> and <paramref name="second"/> name one place once links are followed; a name that is no path is no match.</summary>
    public static bool Same(string first, string second) => Resolved(first) is { } resolved && Is(second, resolved);

    /// <summary>The names among <paramref name="names"/> that name <paramref name="path"/> once links are followed, the path resolved once.</summary>
    public static IEnumerable<string> Naming(IEnumerable<string> names, string path) =>
        Resolved(path) is { } resolved ? names.Where(name => Is(name, resolved)) : [];

    private static bool Is(string name, string resolved) => string.Equals(Resolved(name), resolved, StringComparison.OrdinalIgnoreCase);

    // The place a fully qualified path names once links are followed; null for a name that is no path.
    private static string? Resolved(string path)
    {
        try
        {
            return Path.IsPathFullyQualified(path) ? ResolvedPath.Of(path) : null;
        }
        catch (Exception unusable) when (unusable is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
