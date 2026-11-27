using Autopilot.Core.Platform;

namespace Autopilot.Core.Library.Stores;

/// <summary>
/// The Amazon Games app's installed games, from its install database under
/// <c>%LOCALAPPDATA%\Amazon Games</c>. When that cannot be read, the folders under
/// <c>%LOCALAPPDATA%\Amazon Games\Library</c> and every fixed or removable drive's <c>Amazon Games\Library</c>.
/// </summary>
public sealed class AmazonSource(IDrives drives, HostFolders folders) : IGameSource
{
    public GameSource Source => GameSource.Amazon;

    public async Task<IReadOnlyList<StoreGame>> FindAsync(CancellationToken cancellationToken)
    {
        string? amazon = folders.LocalAppData is { } local ? Path.Join(local, "Amazon Games") : null;
        List<(string? Title, string Folder)>? installed = amazon is null
            ? null
            : await AmazonInstallDatabase.ReadAsync(Path.Join(amazon, "Data", "Games", "Sql", "GameInstallInfo.sqlite"), cancellationToken);
        FoundGames found = new(GameSource.Amazon);
        foreach ((string? title, string folder) in installed ?? LibraryFolders(amazon))
        {
            cancellationToken.ThrowIfCancellationRequested();
            found.Add(title, folder);
        }

        return found.Games;
    }

    private List<(string? Title, string Folder)> LibraryFolders(string? amazon)
    {
        IEnumerable<string> libraries = drives.List()
            .Where(drive => drive.IsLocal)
            .Select(drive => Path.Join(drive.Root, "Amazon Games", "Library"));
        if (amazon is not null)
        {
            libraries = libraries.Prepend(Path.Join(amazon, "Library"));
        }

        return [.. libraries.SelectMany(DrivePaths.Subfolders).Select(folder => ((string?)null, folder))];
    }
}
