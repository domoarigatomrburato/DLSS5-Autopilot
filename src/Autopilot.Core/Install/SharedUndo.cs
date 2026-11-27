using Autopilot.Core.Platform;
using Autopilot.Core.State;
using Autopilot.Core.Storage;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install;

/// <summary>What the shared work of a transaction needs: the places outside the game folder, and where the other installs are found.</summary>
/// <param name="Places">The registry and the layer folder.</param>
/// <param name="Store">The per-game state store, which names every folder the tool installed into.</param>
/// <param name="Records">Reads each such folder's record.</param>
/// <param name="Logger">Where the decisions are logged.</param>
internal sealed record SharedWork(SharedPlaces Places, GameStateStore Store, IInstallRecords Records, ILogger Logger) : IOutsideWork
{
    /// <summary>The registrations and graphics preferences of the install in <paramref name="installFolder"/> undone through <paramref name="disk"/>; its config edits are left.</summary>
    public IOutsideChanges For(InstallDisk disk, string installFolder) => new SharedUndo(disk, Places, new LayerDependents(Store, Records, installFolder, Logger), Logger);

    public Task CleanUpLayerFilesAsync(InstallDisk disk) => new LayerFileCleanup(disk, Places, Logger).RunAsync();
}

/// <summary>
/// Who depends on a shared layer besides the install in <paramref name="installFolder"/>, derived from the install records,
/// never kept as a list: the folders the per-game state store names, each record read from disk once, the first time it is
/// asked. A record that cannot be read counts as a dependent.
/// </summary>
/// <param name="store">Names every folder the tool installed into.</param>
/// <param name="records">Reads their records.</param>
/// <param name="installFolder">The install being undone, which is no dependent of its own.</param>
/// <param name="logger">Where a dependent that could not be read is logged.</param>
internal sealed partial class LayerDependents(GameStateStore store, IInstallRecords records, string installFolder, ILogger logger)
{
    private IReadOnlyList<Dependent>? _others;

    /// <summary>
    /// Whether another install may rely on <paramref name="registration"/>: its record, or the install it replaces, or what it
    /// still releases, lists it; or its record cannot be read now (locked, damaged, a drive not there). Folders are compared as
    /// resolved paths.
    /// </summary>
    public async Task<bool> AnyAsync(LayerRegistration registration)
    {
        _others ??= await OthersAsync();
        Dependent? relying = _others.FirstOrDefault(other => other.Unknown is not null || Lists(other.Record, registration));
        if (relying?.Unknown is { } why)
        {
            LogUnknown(logger, relying.Folder, why);
        }

        return relying is not null;
    }

    /// <summary>Whether <paramref name="record"/> itself, the install it replaces, or what it still releases lists <paramref name="registration"/>.</summary>
    public static bool Lists(InstallRecord? record, LayerRegistration registration) =>
        record is not null
        && (Names(record.Registrations, registration)
            || Names(record.Releasing?.Registrations ?? [], registration)
            || Lists(record.Previous, registration));

    /// <summary>Whether <paramref name="registrations"/> holds <paramref name="registration"/>: the same layer, the same manifest.</summary>
    public static bool Names(IEnumerable<LayerRegistration> registrations, LayerRegistration registration) =>
        registrations.Any(listed => listed.Layer == registration.Layer && SharedPaths.Same(listed.Manifest, registration.Manifest));

    // Every other folder's record, or why it cannot be told; a folder deleted since is none.
    private async Task<IReadOnlyList<Dependent>> OthersAsync()
    {
        var self = GameIdentity.Of(installFolder);
        List<Dependent> others = [];
        foreach (string folder in store.Folders().Where(folder => !GameIdentity.Of(folder).Equals(self)))
        {
            if (await DependentAsync(folder) is { } other)
            {
                others.Add(other);
            }
        }

        return others;
    }

    private async Task<Dependent?> DependentAsync(string folder)
    {
        RecordedInstall? recorded;
        try
        {
            recorded = await records.FindAsync(folder, CancellationToken.None);
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException)
        {
            return new(folder, null, unreadable.Message);
        }

        // A folder deleted is no dependent; one on a drive that is not there now is unknown.
        return recorded switch
        {
            null => Path.GetPathRoot(folder) is { Length: > 0 } root && DrivePaths.IsDirectory(root) ? null : new(folder, null, "its drive is not there"),
            { Record: null } => new(folder, null, $"its install record cannot be read ({recorded.Problem})"),
            _ => new(folder, recorded.Record, null),
        };
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The shared layer is kept: the install in {Folder} may rely on it, and {Why}")]
    private static partial void LogUnknown(ILogger logger, string folder, string why);

    /// <summary>Another install: its record, or why it cannot be told.</summary>
    private sealed record Dependent(string Folder, InstallRecord? Record, string? Unknown);
}

/// <summary>
/// Takes shared registrations and graphics preferences back out, by a record alone: rollback, the release of what an
/// update no longer needs, and uninstall all go through it. Only our own registry values are removed, enumerated and
/// compared as resolved paths, never a reused one, and only when no other install relies on them; the layer files go once
/// neither of their registrations is left. A preference returns only while our token is in it. Config edits are left as
/// they are. Never cancelled.
/// </summary>
/// <param name="disk">Makes the changes.</param>
/// <param name="places">The registry and the layer folder.</param>
/// <param name="dependents">Who else relies on a layer.</param>
/// <param name="logger">Where what was kept, and why, is logged.</param>
internal sealed partial class SharedUndo(InstallDisk disk, SharedPlaces places, LayerDependents dependents, ILogger logger) : IOutsideChanges
{
    public Task<IReadOnlyList<ConfigEdit>> UndoConfigEditsAsync(IReadOnlyList<ConfigEdit> edits, InstallPaths paths, CancellationToken cancellationToken) =>
        Task.FromResult(edits);

    /// <summary>
    /// Restores <paramref name="preferences"/>, the last first: only while the value still says <c>GpuPreference=2</c>. Untouched
    /// since, the old string returns exactly (or the value is deleted when there was none); changed by Windows since, only our
    /// token is removed; our token changed or gone, the value is the person's and stays.
    /// </summary>
    /// <returns>The values that could not be read or restored.</returns>
    public async Task<IReadOnlyList<GraphicsPreferenceEdit>> UndoGraphicsPreferencesAsync(IReadOnlyList<GraphicsPreferenceEdit> preferences, CancellationToken cancellationToken)
    {
        List<GraphicsPreferenceEdit> left = [];
        foreach (GraphicsPreferenceEdit edit in preferences.Reverse())
        {
            try
            {
                await RestoreAsync(edit);
            }
            catch (Exception refused) when (RegistryChange.IsRefusal(refused))
            {
                LogNotRestored(logger, refused, edit.Program);
                left.Insert(0, edit);
            }
        }

        return left;
    }

    /// <summary>
    /// Undoes <paramref name="registrations"/>, the last first: a reused or foreign registration, and one another install relies
    /// on, stay. Then the layer files no registration needs go.
    /// </summary>
    /// <returns>The registrations whose registry value could not be removed.</returns>
    public async Task<IReadOnlyList<LayerRegistration>> UndoRegistrationsAsync(IReadOnlyList<LayerRegistration> registrations, CancellationToken cancellationToken)
    {
        List<LayerRegistration> left = [];
        bool removed = false;
        foreach (LayerRegistration registration in registrations.Reverse().Where(registration => !registration.Reused))
        {
            try
            {
                removed |= await RemoveAsync(registration);
            }
            catch (Exception refused) when (RegistryChange.IsRefusal(refused))
            {
                LogNotRemoved(logger, refused, registration.Manifest);
                left.Insert(0, registration);
            }
        }

        if (removed)
        {
            await new LayerFileCleanup(disk, places, logger).RunAsync();
        }

        return left;
    }

    // Our values for the registration's layer, when no other install relies on it; whether none of ours is left.
    private async Task<bool> RemoveAsync(LayerRegistration registration)
    {
        var kind = LayerKind.Of(registration.Layer);
        if (places.LayerFolder is not { } folder || !SharedPaths.Same(registration.Manifest, kind.ManifestIn(folder)))
        {
            // Never a value we did not create: a record naming another manifest is not followed.
            LogNotOurs(logger, registration.Manifest);
            return false;
        }

        IReadOnlyList<string> ours = kind.OursIn(places.User, folder);
        if (ours.Count == 0)
        {
            return true;
        }

        if (await dependents.AnyAsync(registration))
        {
            LogKept(logger, kind.Shown);
            return false;
        }

        foreach (string name in ours)
        {
            await disk.RunAsync(new DeleteRegistryValue(places.User, kind.Key, name), CancellationToken.None);
        }

        return true;
    }

    private async Task RestoreAsync(GraphicsPreferenceEdit edit)
    {
        IUserRegistry user = places.User;
        if (GraphicsPreferences.NameOf(user, edit.Program) is not { } name
            || user.ReadString(GraphicsPreferences.Key, name) is not { } now
            || GraphicsPreferences.Chosen(now) != GraphicsPreferences.High)
        {
            return;
        }

        string? back = now == edit.Written ? edit.Before : GraphicsPreferences.Without(now);
        DiskChange change = back is null ? new DeleteRegistryValue(user, GraphicsPreferences.Key, name) : new SetRegistryString(user, GraphicsPreferences.Key, name, back);
        await disk.RunAsync(change, CancellationToken.None);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The registration of {Manifest} could not be removed")]
    private static partial void LogNotRemoved(ILogger logger, Exception failure, string manifest);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The Windows graphics setting of {Program} could not be restored")]
    private static partial void LogNotRestored(ILogger logger, Exception failure, string program);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Manifest} is not our layer: its registration is left alone")]
    private static partial void LogNotOurs(ILogger logger, string manifest);

    [LoggerMessage(Level = LogLevel.Information, Message = "Kept the {Layer} layer: another install still uses it")]
    private static partial void LogKept(ILogger logger, string layer);
}

/// <summary>
/// Deletes the layer folder's files no registration of ours needs any more (a manifest whose value is gone, a DLL neither of
/// whose layers is registered) and the temporary files a crashed write left. Best effort: a file another program holds, or
/// one that cannot be deleted, is left for the next time, which is also what the app calls at start.
/// </summary>
/// <param name="disk">Deletes the files.</param>
/// <param name="places">The registry and the layer folder.</param>
/// <param name="logger">Where a file left, and why, is logged.</param>
internal sealed partial class LayerFileCleanup(InstallDisk disk, SharedPlaces places, ILogger logger)
{
    /// <summary>Deletes them; nothing when the registry cannot be read, as every file may still be needed.</summary>
    public async Task RunAsync()
    {
        if (places.LayerFolder is not { } folder || !DrivePaths.IsDirectory(folder))
        {
            return;
        }

        HashSet<RegisteredLayer> registered;
        try
        {
            registered = [.. LayerKind.All.Where(kind => kind.OursIn(places.User, folder).Count > 0).Select(kind => kind.Layer)];
        }
        catch (Exception refused) when (RegistryChange.IsRefusal(refused))
        {
            LogFilesKept(logger, refused, folder);
            return;
        }

        HashSet<string> needed = [.. LayerKind.All.Where(kind => registered.Contains(kind.Layer)).SelectMany(kind => new[] { kind.ManifestIn(folder), kind.LibraryIn(folder) })];
        IEnumerable<string> unneeded = LayerKind.All
            .SelectMany(kind => new[] { kind.ManifestIn(folder), kind.LibraryIn(folder) })
            .Distinct()
            .Where(file => !needed.Contains(file))
            .Concat(DrivePaths.Files(folder).Where(file => AtomicFile.IsTemporary(Path.GetFileName(file))));
        foreach (string file in unneeded.Where(DrivePaths.IsFile))
        {
            await DeleteAsync(file);
        }
    }

    private async Task DeleteAsync(string file)
    {
        try
        {
            await disk.DeleteAsync(file, CancellationToken.None);
        }
        catch (FileHeldException)
        {
            LogFileHeld(logger, file);
        }
        catch (IOException failure)
        {
            LogFileLeft(logger, failure, file);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The layer files in {Folder} are kept: the registry cannot be read")]
    private static partial void LogFilesKept(ILogger logger, Exception failure, string folder);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{File} is in use by a running program: it is deleted at the next start")]
    private static partial void LogFileHeld(ILogger logger, string file);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{File} could not be deleted: it is tried again at the next start")]
    private static partial void LogFileLeft(ILogger logger, Exception failure, string file);
}
