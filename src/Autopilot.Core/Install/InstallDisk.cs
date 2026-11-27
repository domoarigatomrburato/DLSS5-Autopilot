using Autopilot.Core.Storage;

namespace Autopilot.Core.Install;

/// <summary>
/// Every change an install, its commit and its rollback make on disk, each told to the write steps just
/// before it is made. Renames and deletes are retried while another program holds the file; a file still
/// held after that is a <see cref="FileHeldException"/>, and a full drive, however it was wrapped, a
/// <see cref="DiskFullException"/> naming the path.
/// </summary>
/// <param name="records">Saves the record.</param>
/// <param name="steps">Told before each change; tests crash or fill the drive there.</param>
/// <param name="time">The clock the retries wait on.</param>
/// <param name="seam">The rename and delete the changes go through.</param>
/// <param name="entryDeleteWaits">The waits between the tries of an entry's delete (<see cref="DeleteEntryAsync"/>) another program holds.</param>
internal sealed class InstallDisk(InstallRecords records, IWriteSteps steps, TimeProvider time, DiskSeam seam, IReadOnlyList<TimeSpan> entryDeleteWaits)
{
    /// <summary>A disk whose every held file is tried with the atomic write's waits: a moment, five times.</summary>
    public InstallDisk(InstallRecords records, IWriteSteps steps, TimeProvider time, DiskSeam seam)
        : this(records, steps, time, seam, AtomicFile.HeldWaits)
    {
    }

    /// <summary>How many changes other than a record save went ahead.</summary>
    public int Changes { get; private set; }

    /// <summary>Whether any change other than a record save went ahead.</summary>
    public bool Changed => Changes > 0;

    /// <summary>Makes <paramref name="change"/>.</summary>
    /// <exception cref="BackupFailedException">A game's file could not be renamed to its backup name.</exception>
    /// <exception cref="NotVerifiedException">The bytes written are not the verified ones.</exception>
    /// <exception cref="ExplainedException">Windows refused a registry change, or another program holds a file of the layer folder.</exception>
    public Task RunAsync(DiskChange change, CancellationToken cancellationToken) => change switch
    {
        RegistryChange registry => DoAsync(new(registry.Kind, registry.Place), registry.ApplyAsync),
        WriteLayerFile layer => WriteOverAsync(layer, cancellationToken),
        MoveFile move => MoveAsync(move.From, move.To, cancellationToken),
        BackUpFile backup => BackUpAsync(backup.From, backup.To, cancellationToken),
        WriteFile write => WriteAsync(write.Path, write.Facts, write.Source, cancellationToken),
        DeleteFile delete => DeleteAsync(delete.Path, cancellationToken),
        CreateFolderChange folder => CreateFolderAsync(folder.Path),
        _ => throw new NotSupportedException($"no such change: {change}"),
    };

    /// <summary>Saves <paramref name="record"/> as the record of <paramref name="installFolder"/>.</summary>
    public Task SaveRecordAsync(string installFolder, InstallRecord record, CancellationToken cancellationToken) =>
        DoAsync(new(WriteKind.SaveRecord, InstallRecords.PathOf(installFolder)), () => records.SaveAsync(installFolder, record, cancellationToken));

    /// <summary>Removes the record of <paramref name="installFolder"/>.</summary>
    public Task DeleteRecordAsync(string installFolder, CancellationToken cancellationToken) =>
        DoAsync(new(WriteKind.Delete, InstallRecords.PathOf(installFolder)), () => InstallRecords.DeleteAsync(installFolder, cancellationToken));

    /// <summary>Renames <paramref name="from"/> to <paramref name="to"/>, which must not exist.</summary>
    public Task MoveAsync(string from, string to, CancellationToken cancellationToken) =>
        DoAsync(new(WriteKind.Move, from, to), () => RetryAsync(() => seam.Move(from, to), AtomicFile.HeldWaits, cancellationToken));

    /// <summary>Renames <paramref name="from"/> over <paramref name="to"/> in one step, replacing the file there.</summary>
    public Task ReplaceAsync(string from, string to) =>
        DoAsync(new(WriteKind.Replace, from, to), () => RetryAsync(() => seam.Replace(from, to), AtomicFile.HeldWaits, CancellationToken.None));

    /// <summary>Deletes the file at <paramref name="path"/>, clearing its read-only attribute first; a file already gone is deleted.</summary>
    public Task DeleteAsync(string path, CancellationToken cancellationToken) => DeleteAsync(path, AtomicFile.HeldWaits, cancellationToken);

    /// <summary>Deletes an entry's file at <paramref name="path"/> as <see cref="DeleteAsync(string, CancellationToken)"/> does, trying a held file with the entry waits.</summary>
    public Task DeleteEntryAsync(string path) => DeleteAsync(path, entryDeleteWaits, CancellationToken.None);

    private async Task DeleteAsync(string path, IReadOnlyList<TimeSpan> waits, CancellationToken cancellationToken)
    {
        FileInfo file = new(path);
        if (!file.Exists)
        {
            return;
        }

        if (file.IsReadOnly)
        {
            await DoAsync(new(WriteKind.SetAttributes, path), () =>
            {
                Gone(() => file.IsReadOnly = false);
                return Task.CompletedTask;
            });
        }

        await DoAsync(new(WriteKind.Delete, path), () => RetryAsync(() => Gone(() => seam.Delete(path)), waits, cancellationToken));
    }

    /// <summary>Removes the empty folder at <paramref name="path"/>.</summary>
    public Task DeleteFolderAsync(string path) =>
        DoAsync(new(WriteKind.Delete, path), () =>
        {
            Directory.Delete(path, recursive: false);
            return Task.CompletedTask;
        });

    // The game's file renamed to its backup name; a failure is a backup that could not be made.
    private async Task BackUpAsync(string from, string to, CancellationToken cancellationToken)
    {
        try
        {
            await MoveAsync(from, to, cancellationToken);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            throw new BackupFailedException(from, failure);
        }
    }

    // Written through a temporary file, hashed as it is copied, which takes the name through move only with the facts
    // expected; told to the write steps as kind.
    private Task WriteAsync(string path, FileFacts facts, FileSource source, Action<string, string> move, WriteKind kind, CancellationToken cancellationToken)
    {
        FileFacts? copied = null;
        return DoAsync(new(kind, path), () => AtomicFile.WriteAsync(
            path,
            async (stream, token) => copied = await source.CopyToAsync(stream, token),
            (_, _) => facts.Matches(copied) ? Task.CompletedTask : throw new NotVerifiedException(path),
            time,
            move,
            cancellationToken));
    }

    private Task RetryAsync(Action change, IReadOnlyList<TimeSpan> waits, CancellationToken cancellationToken) =>
        AtomicFile.RetryWhileHeldAsync(change, waits, time, cancellationToken);

    private Task WriteAsync(string path, FileFacts facts, FileSource source, CancellationToken cancellationToken) =>
        WriteAsync(path, facts, source, seam.Move, WriteKind.Create, cancellationToken);

    // A layer file is written over what is there; held by a running program, it says so as a layer file.
    private async Task WriteOverAsync(WriteLayerFile layer, CancellationToken cancellationToken)
    {
        try
        {
            await WriteAsync(layer.Path, layer.Facts, layer.Source, seam.Replace, WriteKind.Replace, cancellationToken);
        }
        catch (FileHeldException held)
        {
            throw layer.Held(held);
        }
    }

    private Task CreateFolderAsync(string path) =>
        DoAsync(new(WriteKind.CreateFolder, path), () =>
        {
            _ = Directory.CreateDirectory(path);
            return Task.CompletedTask;
        });

    // A file another step or program removed meanwhile is as good as deleted.
    private static void Gone(Action change)
    {
        try
        {
            change();
        }
        catch (Exception gone) when (gone is FileNotFoundException or DirectoryNotFoundException)
        {
        }
    }

    private async Task DoAsync(WriteStep step, Func<Task> change)
    {
        try
        {
            steps.Before(step);
            if (step.Kind != WriteKind.SaveRecord)
            {
                Changes++;
            }

            await change();
        }
        catch (Exception full) when (IoErrors.IsDiskFull(full) && ExceptionChain.Find<DiskFullException>(full) is null)
        {
            throw new DiskFullException(step.Path, null, full);
        }
        catch (Exception held) when (AtomicFile.IsHeld(held))
        {
            throw new FileHeldException(step.Path, held);
        }
    }
}

/// <summary>The rename and the delete the install's changes go through: the seam a test holds a file with.</summary>
/// <param name="Move">Renames a file to a name that must be free.</param>
/// <param name="Delete">Deletes a file.</param>
/// <param name="Replace">Renames a file over another, replacing it in one step.</param>
internal sealed record DiskSeam(Action<string, string> Move, Action<string> Delete, Action<string, string> Replace)
{
    /// <summary>The file system's own: a move never replaces a file; a replace does.</summary>
    public static DiskSeam Real { get; } = new(static (from, to) => File.Move(from, to, overwrite: false), File.Delete, static (from, to) => File.Move(from, to, overwrite: true));
}

/// <summary>A file another program held through every try: the install fails as locked, naming it.</summary>
internal sealed class FileHeldException(string path, Exception innerException)
    : IOException($"{path} is held by another program: {innerException.Message}", innerException)
{
    /// <summary>The file.</summary>
    public string FilePath { get; } = path;
}

/// <summary>A game's file that could not be backed up: it was left as it is, and the install stops.</summary>
internal sealed class BackupFailedException(string path, Exception innerException)
    : IOException($"{path} could not be backed up: {innerException.Message}", innerException)
{
    /// <summary>The file.</summary>
    public string FilePath { get; } = path;
}

/// <summary>A file whose bytes, written to their temporary name, are not the verified ones: it does not take its name.</summary>
internal sealed class NotVerifiedException(string path) : IOException($"{path}: the bytes written are not the verified ones")
{
    /// <summary>The file.</summary>
    public string FilePath { get; } = path;
}
