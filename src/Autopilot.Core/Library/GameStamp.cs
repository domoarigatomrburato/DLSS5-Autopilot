using Autopilot.Core.Platform;

namespace Autopilot.Core.Library;

/// <summary>
/// What a game's files looked like when it was read: the executable, its folder (a mod dropped
/// beside it), the game folder and the install folder. A folder's time changes only when a direct
/// child is added or removed, so this is a hint, not proof.
/// </summary>
internal sealed record GameStamp(FileMark? Executable, FileMark? ExecutableFolder, FileMark? Folder, FileMark? InstallFolder)
{
    public static GameStamp Of(GameEntry game) => new(
        OfFile(game.Executable),
        OfFolder(game.Executable is { } executable ? Path.GetDirectoryName(executable) : null),
        OfFolder(game.Folder),
        OfFolder(game.InstallFolder));

    private static FileMark? OfFile(string? path) => Mark(path, path => new FileInfo(path) is { Exists: true } file ? new(Seconds(file.LastWriteTimeUtc), file.Length) : null);

    private static FileMark? OfFolder(string? path) => Mark(path, path => new DirectoryInfo(path) is { Exists: true } folder ? new(Seconds(folder.LastWriteTimeUtc), null) : null);

    private static FileMark? Mark(string? path, Func<string, FileMark?> read)
    {
        if (path is null)
        {
            return null;
        }

        try
        {
            return read(path);
        }
        catch (Exception e) when (DrivePaths.IsNotThere(e))
        {
            return null;
        }
    }

    private static long Seconds(DateTime utc) => new DateTimeOffset(utc).ToUnixTimeSeconds();
}

/// <summary>A modification time in whole seconds, and a size for a file.</summary>
internal sealed record FileMark(long Time, long? Size);
