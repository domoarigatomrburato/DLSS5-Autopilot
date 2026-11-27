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
internal sealed class InstallDisk(InstallRecords records, IWriteSteps steps, TimeProvider time, DiskSeam seam)
{
    /// <summary>Whether any change other than a record save went ahead.</summary>
    public bool Changed { get; private set; }

    /// <summary>Makes <paramref name="change"/>.</summary>
    /// <exception cref="BackupFailedException">A game's file could not be renamed to its backup name.</exception>
    /// <exception cref="NotVerifiedException">The bytes written are not the verified ones.</exception>
    public Task RunAsync(DiskChange change, CancellationToken cancellationToken) => change switch
    {
        MoveFile move => MoveAsync(move.From, move.To, cancellationToken),
        BackUpFile backup => BackUpAsync(backup.From, backup.To, cancellationToken),
        WriteFile write => WriteAsync(write.Path, write.Facts, write.Source, cancellationToken),
        DeleteFile delete => DeleteAsync(delete.Path, cancellationToken),
        CreateFolderChange folder => CreateFolderAsync(folder.Path),
        _ => throw new NotSupportedException($"no change of kind {change.GetType().Name}"),
    };

    /// <summary>Saves <paramref name="record"/> as the record of <paramref name="installFolder"/>.</summary>
    public Task SaveRecordAsync(string installFolder, InstallRecord record, CancellationToken cancellationToken) =>
        DoAsync(new(WriteKind.SaveRecord, InstallRecords.PathOf(installFolder)), () => records.SaveAsync(installFolder, record, cancellationToken));

    /// <summary>Removes the record of <paramref name="installFolder"/>.</summary>
    public Task DeleteRecordAsync(string installFolder, CancellationToken cancellationToken) =>
        DoAsync(new(WriteKind.Delete, InstallRecords.PathOf(installFolder)), () => InstallRecords.DeleteAsync(installFolder, cancellationToken));

    /// <summary>Renames <paramref name="from"/> to <paramref name="to"/>, which must not exist.</summary>
    public Task MoveAsync(string from, string to, CancellationToken cancellationToken) =>
        DoAsync(new(WriteKind.Move, from, to), () => AtomicFile.RetryWhileHeldAsync(() => seam.Move(from, to), time, cancellationToken));

    /// <summary>Deletes the file at <paramref name="path"/>, clearing its read-only attribute first; a file already gone is deleted.</summary>
    public async Task DeleteAsync(string path, CancellationToken cancellationToken)
    {
        FileInfo file = new(path);
        if (!file.Exists)
        {
            return;
        }

        if (file.Attributes.HasFlag(FileAttributes.ReadOnly))
        {
            await DoAsync(new(WriteKind.SetAttributes, path), () =>
            {
                Gone(() => file.Attributes &= ~FileAttributes.ReadOnly);
                return Task.CompletedTask;
            });
        }

        await DoAsync(new(WriteKind.Delete, path), () => AtomicFile.RetryWhileHeldAsync(() => Gone(() => seam.Delete(path)), time, cancellationToken));
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

    // Written through a temporary file, hashed as it is copied, which takes the name only with the facts expected.
    private Task WriteAsync(string path, FileFacts facts, FileSource source, CancellationToken cancellationToken)
    {
        FileFacts? copied = null;
        return DoAsync(new(WriteKind.Create, path), () => AtomicFile.WriteAsync(
            path,
            async (stream, token) => copied = await source.CopyToAsync(stream, token),
            (_, _) => facts.Matches(copied) ? Task.CompletedTask : throw new NotVerifiedException(path),
            time,
            seam.Move,
            cancellationToken));
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
                Changed = true;
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
internal sealed record DiskSeam(Action<string, string> Move, Action<string> Delete)
{
    /// <summary>The file system's own: a rename never replaces a file.</summary>
    public static DiskSeam Real { get; } = new(static (from, to) => File.Move(from, to, overwrite: false), File.Delete);
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
