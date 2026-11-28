using Autopilot.Core.Platform;

namespace Autopilot.Core.Install;

/// <summary>The changes that put our layer in place: its files where they differ, then its registry value.</summary>
internal static class LayerFiles
{
    /// <summary>
    /// The DLL and the rewritten manifest of <paramref name="step"/> in <paramref name="folder"/> where they differ, then the
    /// value <paramref name="register"/> set to <c>0</c> (enabled), when given: null when our value is there and enabled.
    /// </summary>
    /// <exception cref="InvalidDataException">The setup's manifest is not one.</exception>
    /// <exception cref="IOException">A layer file cannot be read.</exception>
    public static async Task<IReadOnlyList<DiskChange>> ChangesAsync(RegisterLayer step, LayerKind kind, string folder, IUserRegistry user, string? register, CancellationToken cancellationToken)
    {
        List<DiskChange> made = [];
        await RefreshAsync(made, kind.LibraryIn(folder), step.LibraryFacts, step.Library, cancellationToken);
        if (step.Library is PendingSource)
        {
            // A preview's layer: its manifest is not gathered either, so it is named, not rewritten.
            made.Add(new WriteLayerFile(kind.ManifestIn(folder), PendingSource.Facts, PendingSource.Instance));
        }
        else
        {
            byte[] manifest = kind.Rewrite(step.Manifest);
            await RefreshAsync(made, kind.ManifestIn(folder), FileFacts.Of(manifest), new GeneratedSource(manifest), cancellationToken);
        }

        if (register is not null)
        {
            made.Add(new SetRegistryDword(user, kind.Key, register, 0));
        }

        return made;
    }

    // A layer file already holding the bytes is left alone, so a running program holding it does not fail the install.
    private static async Task RefreshAsync(List<DiskChange> made, string path, FileFacts facts, FileSource source, CancellationToken cancellationToken)
    {
        if (!await facts.IsAtAsync(path, cancellationToken))
        {
            made.Add(new WriteLayerFile(path, facts, source));
        }
    }
}

/// <summary>A ReShade layer registered by something else, which an install reuses rather than adding ours beside it.</summary>
internal static class ForeignLayers
{
    /// <summary>
    /// The first layer registered by something else, for the current user (<paramref name="userNames"/>, read through
    /// <paramref name="userValue"/>) or <paramref name="machine"/>'s 64-bit view, that a program of <paramref name="kind"/>'s
    /// architecture can load: a ReShade manifest outside <paramref name="folder"/>, enabled, there, and of this architecture.
    /// The machine's 32-bit view is never read: a foreign 32-bit layer there calls itself <c>VK_LAYER_reshade</c> and would
    /// clash with ours.
    /// </summary>
    public static string? Find(IRegistry machine, LayerKind kind, string folder, IReadOnlyList<string> userNames, Func<string, int?> userValue) =>
        userNames.FirstOrDefault(name => Usable(kind, folder, name, () => userValue(name)))
        ?? machine.ValueNames(RegistryRoot.LocalMachine, RegistryView.Registry64, kind.Key)
            .FirstOrDefault(name => Usable(kind, folder, name, () => machine.ReadDword(RegistryRoot.LocalMachine, RegistryView.Registry64, kind.Key, name)));

    // A ReShade manifest outside our folder, enabled, there, and of this architecture; the value is read only for such a name.
    private static bool Usable(LayerKind kind, string folder, string name, Func<int?> value) =>
        Path.IsPathFullyQualified(name)
        && Path.GetFileName(name).Contains("reshade", StringComparison.OrdinalIgnoreCase)
        && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
        && !SharedPaths.Same(Path.GetDirectoryName(name) ?? "", folder)
        && value() == 0
        && DrivePaths.IsFile(name)
        && kind.Fits(name);
}
