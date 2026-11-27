using Autopilot.Core.Library.Stores;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Library;

/// <summary>Games the person added by hand: a folder, or an executable.</summary>
public static class ManualGames
{
    private const string ExecutableExtension = ".exe";

    /// <summary>
    /// Reads the game at <paramref name="path"/>: an <c>.exe</c> is the executable and its only
    /// candidate, a folder runs the ranking. The answer says whether it was added, is already in
    /// <paramref name="library"/> (by folder or by executable), holds no executable, or is not there.
    /// Remembering the folder is the caller's.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task<AddResult> AddAsync(string path, IReadOnlyList<GameEntry> library, GameReading context, CancellationToken cancellationToken)
    {
        GameRead read;
        string folder;
        if (IsExecutable(path))
        {
            folder = Path.GetDirectoryName(path)!;
            read = await GameReader.ReadPickedAsync(Found(folder, Path.GetFileName(folder)), path, context, cancellationToken);
        }
        else if (DrivePaths.IsDirectory(path))
        {
            folder = path;
            read = await GameReader.ReadAsync(Found(folder, Path.GetFileName(Path.TrimEndingDirectorySeparator(folder))), context, cancellationToken);
        }
        else
        {
            return new AddPathNotFound(path);
        }

        if (Named(read) is not { } entry)
        {
            return new NoExecutableToAdd(folder);
        }

        return library.FirstOrDefault(game => SamePlace(game.Folder, entry.Folder) || SamePlace(game.Executable, entry.Executable)) is { } existing
            ? new AlreadyListed(existing)
            : new GameAdded(entry, read.Notes);
    }

    /// <summary>A remembered folder read as a <c>Manual</c> game, or null when it holds no executable.</summary>
    internal static async Task<GameRead?> ReadFolderAsync(string folder, GameReading context, CancellationToken cancellationToken)
    {
        GameRead read = await GameReader.ReadAsync(Found(folder, Path.GetFileName(Path.TrimEndingDirectorySeparator(folder))), context, cancellationToken);
        return Named(read) is { } entry ? read with { Entry = entry } : null;
    }

    // Named after the nearest folder above the executable that says something about the game; an
    // emulator is already named after itself by the reading.
    private static GameEntry? Named(GameRead read) =>
        read.Entry.Executable is { } executable ? read.Entry with { Name = read.Entry.Emulator is null ? GameNames.DisplayNameFor(executable) : read.Entry.Name } : null;

    private static StoreGame Found(string folder, string name) => new(name, folder, GameSource.Manual, null);

    private static bool IsExecutable(string path) =>
        Path.IsPathFullyQualified(path) && path.EndsWith(ExecutableExtension, StringComparison.OrdinalIgnoreCase) && DrivePaths.IsFile(path);

    private static bool SamePlace(string? listed, string? added) =>
        listed is not null && added is not null && string.Equals(ResolvedPath.Of(listed), ResolvedPath.Of(added), StringComparison.OrdinalIgnoreCase);
}
