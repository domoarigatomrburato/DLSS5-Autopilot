using System.Text.Json;
using System.Text.Json.Nodes;
using Autopilot.Core.Graphics;
using Autopilot.Core.Storage;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install;

/// <summary>
/// Reads and writes the install record, <c>invidia-claritatis.json</c> in an install folder. The file is
/// untrusted on read: a record that cannot be used is <see cref="InstallState.Unreadable"/> with the
/// reason, never no record, and an entry naming a path outside the game folder is dropped, never followed.
/// </summary>
/// <param name="logger">Where dropped entries and unusable records are logged.</param>
/// <param name="time">The clock the atomic write's retries wait on.</param>
public sealed partial class InstallRecords(ILogger<InstallRecords> logger, TimeProvider time) : IInstallRecords
{
    /// <summary>The record format this build reads and writes.</summary>
    internal const int Format = 1;

    /// <summary>A record larger than this is not one of ours.</summary>
    internal const int MaxBytes = 1024 * 1024;

    /// <summary>
    /// The record in <paramref name="folder"/>: null when there is none; <see cref="InstallState.Unreadable"/>
    /// when it is damaged, the wrong shape, newer than this build, or cannot be read right now. Reading never writes.
    /// The file is read synchronously, on the calling thread: call it from a background thread.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public Task<RecordedInstall?> FindAsync(string folder, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Find(folder));
    }

    /// <summary>Writes <paramref name="record"/> as the record of <paramref name="installFolder"/>, atomically, in format 1.</summary>
    /// <exception cref="IOException">The write or the move failed.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder or the record cannot be written.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public Task SaveAsync(string installFolder, InstallRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        InstallRecord stamped = record with { FormatVersion = Format };
        string path = PathOf(installFolder);
        Writable(path);
        return AtomicFile.WriteAsync(
            path,
            (file, token) => JsonSerializer.SerializeAsync(file, stamped, InstallJsonContext.Default.InstallRecord, token),
            check: null,
            time,
            cancellationToken);
    }

    /// <summary>Removes the record of <paramref name="installFolder"/>; one that is not there is not an error.</summary>
    /// <exception cref="IOException">The record is held by another program.</exception>
    /// <exception cref="UnauthorizedAccessException">The record cannot be removed.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static Task DeleteAsync(string installFolder, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = PathOf(installFolder);
        Writable(path);
        try
        {
            File.Delete(path);
        }
        catch (DirectoryNotFoundException)
        {
            // No folder, no record.
        }

        return Task.CompletedTask;
    }

    /// <summary>The record file of the install folder <paramref name="folder"/>.</summary>
    internal static string PathOf(string folder) => Path.Join(folder, KnownFiles.InstallRecord);

    // Read-only is cleared on a file of ours before it is replaced or deleted.
    private static void Writable(string path)
    {
        try
        {
            FileAttributes attributes = File.GetAttributes(path);
            if (attributes.HasFlag(FileAttributes.ReadOnly) && !attributes.HasFlag(FileAttributes.Directory))
            {
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            }
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            // No record yet: nothing to clear.
        }
    }

    private RecordedInstall? Find(string folder)
    {
        VersionedRead read = VersionedJson.Read(PathOf(folder), MaxBytes, InstallRecord.VersionKey, Format);
        return read.State switch
        {
            JsonFileState.Missing => null,
            JsonFileState.Present => Parse(folder, read.Document!),
            JsonFileState.Newer => Unusable(folder, RecordProblem.Newer),
            JsonFileState.Unreadable => Unusable(folder, RecordProblem.Inaccessible),
            _ => Unusable(folder, RecordProblem.Damaged),
        };
    }

    private RecordedInstall Parse(string folder, JsonObject document) =>
        RecordShape.Read(document, folder, logger) ?? Unusable(folder, RecordProblem.WrongShape);

    private RecordedInstall Unusable(string folder, RecordProblem problem)
    {
        LogUnusable(logger, folder, problem);
        return new RecordedInstall(InstallState.Unreadable, []) { Problem = problem };
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The install record in {Folder} cannot be used: {Problem}")]
    private static partial void LogUnusable(ILogger logger, string folder, RecordProblem problem);
}
