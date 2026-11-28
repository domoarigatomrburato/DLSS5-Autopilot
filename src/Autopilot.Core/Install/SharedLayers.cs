using System.Text.Json;
using System.Text.Json.Nodes;
using Autopilot.Core.Components.Sources;
using Autopilot.Core.Executables;
using Autopilot.Core.Platform;
using Autopilot.Core.Storage;

namespace Autopilot.Core.Install;

/// <summary>
/// The places outside the game folder an install may change: the current user's registry, which it
/// writes, the machine's, which it only reads (a foreign layer to reuse), and the layer folder,
/// <c>%LOCALAPPDATA%\invidia-claritatis\reshade-vulkan</c>, the one folder outside a game folder it writes
/// files into.
/// </summary>
/// <param name="User">The current user's registry.</param>
/// <param name="Machine">The machine's registry, read in its 64-bit view only.</param>
/// <param name="LayerFolder">The layer folder, full; null when the machine names no local app data folder.</param>
internal sealed record SharedPlaces(IUserRegistry User, IRegistry Machine, string? LayerFolder)
{
    /// <summary>The layer folder below <paramref name="folders"/>' local app data; null when it is not set.</summary>
    public static string? LayerFolderOf(HostFolders folders)
    {
        ArgumentNullException.ThrowIfNull(folders);
        return folders.LocalAppData is { } local ? Path.Join(local, "invidia-claritatis", "reshade-vulkan") : null;
    }
}

/// <summary>What one shared layer is: its registry key, its files in the layer folder, and what its manifest says.</summary>
/// <param name="Layer">The layer.</param>
/// <param name="Key">The key below <c>HKEY_CURRENT_USER</c> (and <c>HKEY_LOCAL_MACHINE</c>) its registrations are values of.</param>
/// <param name="Library">The DLL's file name.</param>
/// <param name="Manifest">The manifest's file name.</param>
/// <param name="Section">The manifest's object holding <c>library_path</c>.</param>
/// <param name="Name">The layer name the manifest is given; null when the setup's own name stays.</param>
/// <param name="Bitness">The architecture of the programs that load it.</param>
/// <param name="Shown">The layer as a person reads it ("Vulkan", "OpenXR").</param>
internal sealed record LayerKind(RegisteredLayer Layer, string Key, string Library, string Manifest, string Section, string? Name, Bitness Bitness, string Shown)
{
    /// <summary>Where Vulkan's implicit layers are registered.</summary>
    public const string VulkanKey = @"Software\Khronos\Vulkan\ImplicitLayers";

    /// <summary>Where OpenXR's implicit API layers are registered.</summary>
    public const string OpenXrKey = @"Software\Khronos\OpenXR\1\ApiLayers\Implicit";

    // A manifest larger than this is not ReShade's.
    private const int MaxManifestBytes = 64 * 1024;

    /// <summary>Every layer an install registers.</summary>
    public static IReadOnlyList<LayerKind> All { get; } =
    [
        new(RegisteredLayer.VulkanLayer64, VulkanKey, ReShadeInstaller.Runtime64, ReShadeInstaller.Manifest64, "layer", null, Bitness.Bits64, "Vulkan"),
        // The loader drops a second layer of one name, so the 32-bit layer carries its own (install spec, "What is registered").
        new(RegisteredLayer.VulkanLayer32, VulkanKey, ReShadeInstaller.Runtime32, ReShadeInstaller.Manifest32, "layer", "VK_LAYER_reshade32", Bitness.Bits32, "Vulkan"),
        new(RegisteredLayer.OpenXr, OpenXrKey, ReShadeInstaller.Runtime64, "ReShade64_XR.json", "api_layer", null, Bitness.Bits64, "OpenXR"),
    ];

    /// <summary>The kind of <paramref name="layer"/>.</summary>
    public static LayerKind Of(RegisteredLayer layer) => All.First(kind => kind.Layer == layer);

    /// <summary>Our manifest's full path in <paramref name="layerFolder"/>: the registry value's name.</summary>
    public string ManifestIn(string layerFolder) => Path.Join(layerFolder, Manifest);

    /// <summary>Our DLL's full path in <paramref name="layerFolder"/>.</summary>
    public string LibraryIn(string layerFolder) => Path.Join(layerFolder, Library);

    /// <summary>The value names under this layer's key in <paramref name="user"/> that name our manifest in <paramref name="layerFolder"/>.</summary>
    /// <exception cref="IOException">The key cannot be read.</exception>
    public IReadOnlyList<string> OursIn(IUserRegistry user, string layerFolder) => [.. SharedPaths.Naming(user.ValueNames(Key), ManifestIn(layerFolder))];

    /// <summary>
    /// The setup's manifest with its <c>library_path</c> set to <c>.\&lt;dll&gt;</c>, so a moved folder cannot leave
    /// a dangling path, and the 32-bit layer's name set; everything else as it was.
    /// </summary>
    /// <exception cref="InvalidDataException">The bytes are not a manifest of this layer.</exception>
    public byte[] Rewrite(ReadOnlyMemory<byte> manifest)
    {
        if (UntrustedJson.ParseObject(manifest.ToArray()) is not { } document || document[Section] is not JsonObject section)
        {
            throw new InvalidDataException($"{Manifest} from ReShade's setup is not a layer manifest");
        }

        section["library_path"] = @".\" + Library;
        if (Name is not null)
        {
            section["name"] = Name;
        }

        using MemoryStream bytes = new();
        using (Utf8JsonWriter writer = new(bytes, new JsonWriterOptions { Indented = true }))
        {
            document.WriteTo(writer);
        }

        return bytes.ToArray();
    }

    /// <summary>
    /// Whether a registered manifest at <paramref name="manifest"/> can be loaded by this layer's programs: its
    /// DLL's name (or, failing that, its own) says 32 or 64, or nothing, which is taken as either.
    /// </summary>
    public bool Fits(string manifest)
    {
        string name = Path.GetFileName(manifest);
        FileBytes read = BoundedFile.Read(manifest, MaxManifestBytes);
        if (read.Bytes is { } content
            && UntrustedJson.ParseObject(content) is { } document
            && document[Section] is JsonObject section
            && section["library_path"] is JsonValue value
            && value.TryGetValue(out string? library))
        {
            name = PathParts.Split(library).LastOrDefault() is { Length: > 0 } file ? file : name;
        }

        bool is32 = name.Contains("32", StringComparison.Ordinal);
        bool is64 = name.Contains("64", StringComparison.Ordinal);
        return Bitness == Bitness.Bits32 ? is32 || !is64 : is64 || !is32;
    }
}
