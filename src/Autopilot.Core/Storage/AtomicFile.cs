using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Storage;

/// <summary>
/// Replaces a file whole, so a crash or a failed write never leaves it half written: the one atomic
/// write every file the tool writes whole goes through.
/// </summary>
internal static partial class AtomicFile
{
    /// <summary>
    /// What every temporary name carries after the target's name, so a leftover is known as ours;
    /// short, so a target near Windows' 260-character path limit keeps room for it.
    /// </summary>
    private const string Marker = ".ic-";

    /// <summary>An antivirus or an indexer holds a file for a moment: the replace is tried this many times.</summary>
    private const int MoveTries = 6;

    /// <summary>The waits between the tries of a rename or delete another program holds: 100 ms, so 6 tries in all.</summary>
    internal static IReadOnlyList<TimeSpan> HeldWaits { get; } = [.. Enumerable.Repeat(TimeSpan.FromMilliseconds(100), MoveTries - 1)];

    /// <summary>
    /// Writes the content <paramref name="write"/> produces to a temporary file in the target's
    /// folder, flushes it to disk, runs <paramref name="check"/> on it when given, then moves it over
    /// <paramref name="path"/>, creating the folder when needed. A target held by another program is
    /// tried again, 100 ms apart, up to 6 times. On any failure the old file is left whole and the
    /// temporary file is removed when it can be; the target is never written in place.
    /// </summary>
    /// <param name="path">The file to replace.</param>
    /// <param name="write">Writes the whole content to the stream it is given.</param>
    /// <param name="check">Runs on the flushed temporary file (its path is passed) before it replaces the target; a throw keeps the target as it was.</param>
    /// <param name="time">The clock the waits between tries run on.</param>
    /// <param name="cancellationToken">Stops the write; the temporary file is removed.</param>
    /// <exception cref="IOException">The write or the move failed.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder or the target cannot be written.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static Task WriteAsync(
        string path,
        Func<Stream, CancellationToken, Task> write,
        Func<string, CancellationToken, Task>? check,
        TimeProvider time,
        CancellationToken cancellationToken) =>
        WriteAsync(path, write, check, time, static (temporary, target) => File.Move(temporary, target, overwrite: true), cancellationToken);

    /// <summary>The same, replacing the target through <paramref name="move"/>: the seam a test holds the target with.</summary>
    internal static async Task WriteAsync(
        string path,
        Func<Stream, CancellationToken, Task> write,
        Func<string, CancellationToken, Task>? check,
        TimeProvider time,
        Action<string, string> move,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(move);
        string target = Path.GetFullPath(path);
        string folder = Path.GetDirectoryName(target)!;
        _ = Directory.CreateDirectory(folder);
        string temporary = Path.Combine(folder, TemporaryName(Path.GetFileName(target)));
        try
        {
            await using (FileStream file = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await write(file, cancellationToken);
                await file.FlushAsync(cancellationToken);
                // There is no asynchronous fsync, so this blocks; without it a crash after the move
                // can leave an empty file under the target's name.
                RandomAccess.FlushToDisk(file.SafeFileHandle);
            }

            if (check is not null)
            {
                await check(temporary, cancellationToken);
            }

            await MoveAsync(temporary, path, time, move, cancellationToken);
        }
        catch
        {
            Remove(temporary);
            throw;
        }
    }

    /// <summary>Whether <paramref name="fileName"/> is a temporary name this helper makes, such as one a crashed run left behind.</summary>
    internal static bool IsTemporary(string fileName) => TemporaryPattern().IsMatch(fileName);

    // <target name>.ic-<process id>-<8 hex digits>.tmp
    private static string TemporaryName(string targetName) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{targetName}{Marker}{Environment.ProcessId}-{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4))}.tmp");

    [GeneratedRegex(@"\A.+\.ic-[0-9]{1,10}-[0-9a-f]{8}\.tmp\z", RegexOptions.CultureInvariant)]
    private static partial Regex TemporaryPattern();

    // Windows asks for a write-through rename (MOVEFILE_WRITE_THROUGH) here; that needs a Windows API,
    // so it lands with the Windows implementations in M7.
    private static Task MoveAsync(string temporary, string path, TimeProvider time, Action<string, string> move, CancellationToken cancellationToken) =>
        RetryWhileHeldAsync(() => move(temporary, path), HeldWaits, time, cancellationToken);

    /// <summary>
    /// Runs <paramref name="change"/> (a rename, a delete), trying again after each wait of <paramref name="waits"/>
    /// in turn while another program holds the file, as antivirus and indexers do for a moment. The last failure is thrown.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static async Task RetryWhileHeldAsync(Action change, IReadOnlyList<TimeSpan> waits, TimeProvider time, CancellationToken cancellationToken)
    {
        for (int tried = 0; ; tried++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                change();
                return;
            }
            catch (Exception held) when (tried < waits.Count && IsHeld(held))
            {
                await Task.Delay(waits[tried], time, cancellationToken);
            }
        }
    }

    /// <summary>Whether <paramref name="failure"/> is how Windows reports a change to a file another program holds open: either way.</summary>
    internal static bool IsHeld(Exception failure) =>
        failure is UnauthorizedAccessException || IoErrors.IsSharingViolation(failure);

    // A temporary file that cannot be removed is left behind rather than hiding why the write failed.
    private static void Remove(string temporary)
    {
        try
        {
            File.Delete(temporary);
        }
        catch (Exception e) when (DrivePaths.IsNotThere(e))
        {
            // The caller gets the write's own failure.
        }
    }
}
