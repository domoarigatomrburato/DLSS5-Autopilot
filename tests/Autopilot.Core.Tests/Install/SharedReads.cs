using System.Text.Json.Nodes;
using Autopilot.Core.Install;

namespace Autopilot.Core.Tests.Install;

/// <summary>What a bed with shared places holds outside the game folder, as strings tests compare.</summary>
internal static class SharedReads
{
    /// <summary>The layer folder's file names, sorted and joined by <c>|</c>; empty when the folder is not there.</summary>
    public static string LayerFiles(this TransactionBed bed) =>
        Directory.Exists(bed.LayerFolder()) ? string.Join("|", Directory.EnumerateFiles(bed.LayerFolder()).Select(Path.GetFileName).Order(StringComparer.Ordinal)) : "";

    /// <summary>Whether the layer folder's <paramref name="name"/> holds <paramref name="content"/>.</summary>
    public static bool LayerFileHolds(this TransactionBed bed, string name, byte[] content) => File.ReadAllBytes(bed.LayerFile(name)).AsSpan().SequenceEqual(content);

    /// <summary>The program's graphics preference value; null when it has none.</summary>
    public static string? PreferenceOf(this TransactionBed bed, string program = "Game.exe") => bed.User().Value(SharedBeds.PreferenceKey, bed.Program(program)) as string;

    /// <summary>Our manifest's registry value for <paramref name="layer"/>; null when there is none.</summary>
    public static object? Registered(this TransactionBed bed, RegisteredLayer layer) =>
        bed.User().Value(layer == RegisteredLayer.OpenXr ? SharedBeds.OpenXrKey : SharedBeds.VulkanKey, bed.LayerFile(ManifestName(layer)));

    public static string ManifestName(RegisteredLayer layer) => layer switch
    {
        RegisteredLayer.OpenXr => "ReShade64_XR.json",
        RegisteredLayer.VulkanLayer32 => "ReShade32.json",
        _ => "ReShade64.json",
    };

    /// <summary>A string of the manifest in the layer folder, found by its property names.</summary>
    public static string? ManifestValue(this TransactionBed bed, RegisteredLayer layer, params string[] names)
    {
        var node = JsonNode.Parse(File.ReadAllBytes(bed.LayerFile(ManifestName(layer))));
        foreach (string name in names)
        {
            node = node?[name];
        }

        return (string?)node;
    }

    /// <summary>The record's registrations as <see cref="Registration"/> writes them, one per line.</summary>
    public static async Task<string> RegistrationsAsync(this TransactionBed bed, CancellationToken cancellationToken) =>
        string.Join("\n", (await bed.RecordAsync(cancellationToken)).Registrations.Select(Registration));

    /// <summary>Our registration of <paramref name="layer"/>, in the bed's layer folder.</summary>
    public static string Ours(this TransactionBed bed, RegisteredLayer layer) => Registration(new(layer, false, bed.LayerFile(ManifestName(layer))));

    /// <summary>A reused registration of the manifest <paramref name="manifest"/>.</summary>
    public static string Reused(RegisteredLayer layer, string manifest) => Registration(new(layer, true, manifest));

    private static string Registration(LayerRegistration registration) => $"{registration.Layer} {(registration.Reused ? "reused" : "ours")} {registration.Manifest}";

    /// <summary>The record's graphics preference edits as <c>program: before -> written</c>, one per line.</summary>
    public static async Task<string> PreferencesAsync(this TransactionBed bed, CancellationToken cancellationToken) =>
        Edits((await bed.RecordAsync(cancellationToken)).GraphicsPreferences);

    /// <summary>The edits still to release on the record; null when there is nothing to release.</summary>
    public static async Task<string?> ReleasingAsync(this TransactionBed bed, CancellationToken cancellationToken) =>
        (await bed.RecordAsync(cancellationToken)).Releasing is { } releasing ? Edits(releasing.GraphicsPreferences) : null;

    /// <summary>An edit of the game's value: what was there before and what was written.</summary>
    public static string Edit(this TransactionBed bed, string? before, string written, string program = "Game.exe") => Edit(new(bed.Program(program), before, written));

    private static string Edits(IEnumerable<GraphicsPreferenceEdit> edits) => string.Join("\n", edits.Select(Edit));

    private static string Edit(GraphicsPreferenceEdit edit) => $"{edit.Program}: {edit.Before ?? "(none)"} -> {edit.Written}";

    /// <summary>The registry and the layer folder as one string: what a test compares before and after.</summary>
    public static string OutsideSnapshot(this TransactionBed bed) =>
        bed.User().Snapshot() + "\n--\n" + string.Join("\n", Directory.Exists(bed.LayerFolder())
            ? Directory.EnumerateFiles(bed.LayerFolder()).Order(StringComparer.Ordinal).Select(file => $"{Path.GetFileName(file)} {Convert.ToHexString(File.ReadAllBytes(file))[..16]}")
            : []);

    /// <summary>Whether a line the bed logged contains <paramref name="text"/>.</summary>
    public static bool Says(this TransactionBed bed, string text) =>
        bed.Log.Collector.GetSnapshot().Any(line => line.Message.Contains(text, StringComparison.Ordinal));

    /// <summary>The committed install's note or warning containing every one of <paramref name="texts"/>; null when there is none.</summary>
    public static string? Said(this InstallCommitted committed, params string[] texts) =>
        committed.Notes.Concat(committed.Warnings).FirstOrDefault(said => texts.All(text => said.Contains(text, StringComparison.Ordinal)));
}
