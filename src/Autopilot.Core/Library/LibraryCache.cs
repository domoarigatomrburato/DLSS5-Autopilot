using System.Diagnostics.CodeAnalysis;

namespace Autopilot.Core.Library;

/// <summary>
/// The library kept between launches, so a launch with nothing changed skips the scan. Nothing here
/// is load-bearing: a cache that is missing, unreadable, stale or empty means a full scan.
/// </summary>
public static class LibraryCache
{
    /// <summary>The shape of the file; a file of another shape is not used.</summary>
    private const int Schema = 3;

    /// <summary>
    /// Saves <paramref name="games"/> with a stamp of each one's files, atomically. An I/O or access
    /// failure is the result, never an exception, and leaves no temporary file behind.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static Task<CacheSaveResult> SaveAsync(string path, CacheKey key, IReadOnlyList<GameEntry> games, CancellationToken cancellationToken) =>
        SaveAsync(path, key, games, static file => file, cancellationToken);

    /// <summary>Saves through <paramref name="through"/>, which wraps the temporary file's stream: the seam a test fills the drive with.</summary>
    internal static async Task<CacheSaveResult> SaveAsync(
        string path, CacheKey key, IReadOnlyList<GameEntry> games, Func<Stream, Stream> through, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(games);
        CacheFile content = new(Schema, key.ToolVersion, key.GpuSm, [.. games.Select(CachedEntry.Of)]);
        try
        {
            await content.WriteAsync(path, through, cancellationToken);
            return new CacheSaveResult(null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new CacheSaveResult(e);
        }
    }

    /// <summary>
    /// The saved library, or null when there is none to use: no file, one that cannot be read, of
    /// another shape, saved under another <paramref name="key"/>, or holding no games. A game whose
    /// folder is gone is dropped; one whose files changed, or whose executable Windows refuses to
    /// open, is read again as a scan reads it; the rest come back as saved, with their emulator
    /// found again and the person's choices applied as a scan applies them. Then one entry per
    /// executable, in the library's order. Games are read on the calling thread, so call it from a
    /// background thread.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task<CachedLibrary?> LoadAsync(string path, CacheKey key, GameReading reading, TimeProvider clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(clock);
        CacheFile? file = await CacheFile.ReadAsync(path, cancellationToken);
        return IsUsable(file, key) ? await new CacheReload(reading, clock).RunAsync(file.Games, cancellationToken) : null;
    }

    /// <summary>Deletes the saved library, so the next launch scans from scratch; a file that cannot be deleted is left.</summary>
    public static void Forget(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Left in place, it is still guarded by its key and its stamps.
        }
    }

    // What the tool believes about a game changes between releases, and the compatibility rows are
    // read against the card in the machine, so either changing means a full scan.
    private static bool IsUsable([NotNullWhen(true)] CacheFile? file, CacheKey key) =>
        file is not null
        && file.Schema == Schema && file.ToolVersion == key.ToolVersion && file.GpuSm == key.GpuSm
        && file.Games.Count > 0 && file.IsWellFormed;
}
