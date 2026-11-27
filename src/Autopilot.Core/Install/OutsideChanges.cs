namespace Autopilot.Core.Install;

/// <summary>
/// Undoes what an install recorded beyond its files: config keys, the Windows graphics preference and the
/// shared layer registrations. Uninstall calls it at its steps 1 and 5; each list gives back what it could
/// not undo, which stays on the record.
/// </summary>
internal interface IOutsideChanges
{
    /// <summary>Puts back the keys of <paramref name="edits"/>, in files below <paramref name="paths"/>' game folder.</summary>
    /// <returns>The edits left as they are.</returns>
    Task<IReadOnlyList<ConfigEdit>> UndoConfigEditsAsync(IReadOnlyList<ConfigEdit> edits, InstallPaths paths, CancellationToken cancellationToken);

    /// <summary>Puts back the graphics preference values of <paramref name="preferences"/>.</summary>
    /// <returns>The values left as they are.</returns>
    Task<IReadOnlyList<GraphicsPreferenceEdit>> UndoGraphicsPreferencesAsync(IReadOnlyList<GraphicsPreferenceEdit> preferences, CancellationToken cancellationToken);

    /// <summary>Removes the registrations of <paramref name="registrations"/> no other install needs.</summary>
    /// <returns>The registrations left as they are.</returns>
    Task<IReadOnlyList<LayerRegistration>> UndoRegistrationsAsync(IReadOnlyList<LayerRegistration> registrations, CancellationToken cancellationToken);
}

/// <summary>
/// Today's outside changes: none is undone yet, so every one stays on the record. Config edits come with the
/// route plans that make them, the registrations and the graphics preference with their own sub-task.
/// </summary>
internal sealed class OutsideChanges : IOutsideChanges
{
    private OutsideChanges()
    {
    }

    /// <summary>The one instance.</summary>
    public static OutsideChanges NotUndoneYet { get; } = new();

    /// <summary>Whether <paramref name="record"/> names any change outside its files: a record that does is not deleted while it does.</summary>
    public static bool Carries(InstallRecord record) =>
        record.ConfigEdits.Count > 0 || record.Registrations.Count > 0 || record.GraphicsPreferences.Count > 0;

    public Task<IReadOnlyList<ConfigEdit>> UndoConfigEditsAsync(IReadOnlyList<ConfigEdit> edits, InstallPaths paths, CancellationToken cancellationToken) =>
        Task.FromResult(edits);

    public Task<IReadOnlyList<GraphicsPreferenceEdit>> UndoGraphicsPreferencesAsync(IReadOnlyList<GraphicsPreferenceEdit> preferences, CancellationToken cancellationToken) =>
        Task.FromResult(preferences);

    public Task<IReadOnlyList<LayerRegistration>> UndoRegistrationsAsync(IReadOnlyList<LayerRegistration> registrations, CancellationToken cancellationToken) =>
        Task.FromResult(registrations);
}

/// <summary>A record's changes outside its files undone through an <see cref="IOutsideChanges"/>, the rollback's and the uninstall's alike.</summary>
internal static class OutsideChangesUndo
{
    /// <summary><paramref name="record"/> with only the config edits <paramref name="outside"/> could not undo.</summary>
    public static async Task<InstallRecord> ConfigEditsUndoneAsync(this IOutsideChanges outside, InstallRecord record, InstallPaths paths) =>
        record with { ConfigEdits = await outside.UndoConfigEditsAsync(record.ConfigEdits, paths, CancellationToken.None) };

    /// <summary><paramref name="record"/> with only the graphics preferences and registrations <paramref name="outside"/> could not undo.</summary>
    public static async Task<InstallRecord> SystemChangesUndoneAsync(this IOutsideChanges outside, InstallRecord record) =>
        record with
        {
            GraphicsPreferences = await outside.UndoGraphicsPreferencesAsync(record.GraphicsPreferences, CancellationToken.None),
            Registrations = await outside.UndoRegistrationsAsync(record.Registrations, CancellationToken.None),
        };
}
