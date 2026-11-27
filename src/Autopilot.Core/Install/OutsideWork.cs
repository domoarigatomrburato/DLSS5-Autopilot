using Autopilot.Core.State;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install;

/// <summary>
/// Makes the <see cref="IOutsideChanges"/> of one run: its changes made through that run's disk, so the write steps, a crash
/// and a held file reach them, and "may this registration go?" asked of every install but the one being undone.
/// </summary>
internal interface IOutsideWork
{
    /// <summary>The undo of the changes outside the files of the install in <paramref name="installFolder"/>, made through <paramref name="disk"/>.</summary>
    IOutsideChanges For(InstallDisk disk, string installFolder);

    /// <summary>
    /// Deletes, through <paramref name="disk"/>, the shared layer files no registration needs any more and the temporary files a
    /// crashed write left; best effort, as the app does at start.
    /// </summary>
    Task CleanUpLayerFilesAsync(InstallDisk disk);
}

/// <summary>The outside work of a transaction or an uninstall.</summary>
internal static class OutsideWork
{
    /// <summary>
    /// The registrations and graphics preferences undone over <paramref name="shared"/>; with none (no registry, as on a
    /// system without one), nothing outside the files is undone and all of it stays on the record.
    /// </summary>
    public static IOutsideWork Of(SharedPlaces? shared, GameStateStore store, IInstallRecords records, ILogger logger) =>
        shared is null ? NoOutsideWork.Instance : new SharedWork(shared, store, records, logger);
}

/// <summary>
/// No change outside the files is undone, every one staying on the record: the outside work without a registry. Config
/// edits come with the route plans that make them.
/// </summary>
internal sealed class NoOutsideWork : IOutsideWork, IOutsideChanges
{
    private NoOutsideWork()
    {
    }

    /// <summary>The one instance.</summary>
    public static NoOutsideWork Instance { get; } = new();

    public IOutsideChanges For(InstallDisk disk, string installFolder) => this;

    public Task CleanUpLayerFilesAsync(InstallDisk disk) => Task.CompletedTask;

    public Task<IReadOnlyList<ConfigEdit>> UndoConfigEditsAsync(IReadOnlyList<ConfigEdit> edits, InstallPaths paths, CancellationToken cancellationToken) =>
        Task.FromResult(edits);

    public Task<IReadOnlyList<GraphicsPreferenceEdit>> UndoGraphicsPreferencesAsync(IReadOnlyList<GraphicsPreferenceEdit> preferences, CancellationToken cancellationToken) =>
        Task.FromResult(preferences);

    public Task<IReadOnlyList<LayerRegistration>> UndoRegistrationsAsync(IReadOnlyList<LayerRegistration> registrations, CancellationToken cancellationToken) =>
        Task.FromResult(registrations);
}

/// <summary>What a rollback over an earlier install takes back out of what it added outside the files.</summary>
internal static class OutsideRollback
{
    /// <summary>
    /// <paramref name="previous"/>, the install the rolled-back <paramref name="record"/> returns to, once what <paramref name="record"/>
    /// made outside its files that <paramref name="previous"/> does not rely on is undone. What could not be undone is added to
    /// what <paramref name="previous"/> still releases, for the next transaction to finish.
    /// </summary>
    public static async Task<InstallRecord> PreviousBackAsync(this IOutsideChanges outside, InstallRecord record, InstallRecord previous) =>
        Releases.Leftover(record, previous) is { } added && await outside.UndoAsync(added) is { } left
            ? previous with
            {
                Releasing = new([.. previous.Releasing?.Registrations ?? [], .. left.Registrations], [.. previous.Releasing?.GraphicsPreferences ?? [], .. left.GraphicsPreferences]),
            }
            : previous;
}
