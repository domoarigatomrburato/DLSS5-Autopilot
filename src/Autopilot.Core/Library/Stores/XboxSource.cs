using Autopilot.Core.Platform;

namespace Autopilot.Core.Library.Stores;

/// <summary>
/// Game Pass and Microsoft Store PC games: the folders under every fixed or removable drive's <c>XboxGames</c> and
/// <c>Program Files\ModifiableWindowsApps</c>. The game is the <c>Content</c> subfolder when there is
/// one; the store's pick is the PC executable <c>MicrosoftGame.config</c> names. Named after the folder.
/// </summary>
public sealed class XboxSource(IDrives drives) : IGameSource
{
    // Cloud saves in empty containers, and a large runtime tree that is not a game.
    private static readonly string[] _notGames = ["GameSave", "Minecraft Launcher"];

    public GameSource Source => GameSource.Xbox;

    public async Task<IReadOnlyList<StoreGame>> FindAsync(CancellationToken cancellationToken)
    {
        FoundGames found = new();
        foreach (string container in Containers())
        {
            cancellationToken.ThrowIfCancellationRequested();
            string name = Path.GetFileName(container);
            if (_notGames.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            string content = Path.Join(container, "Content");
            string folder = DrivePaths.IsDirectory(content) ? content : container;
            found.Add(new(name, folder, GameSource.Xbox, await MicrosoftGameConfig.PcExecutableAsync(folder, cancellationToken)));
        }

        return found.Games;
    }

    private IEnumerable<string> Containers() =>
        drives.List()
            .Where(drive => drive.Kind is DriveKind.Fixed or DriveKind.Removable)
            .SelectMany(drive => new[] { Path.Join(drive.Root, "XboxGames"), Path.Join(drive.Root, "Program Files", "ModifiableWindowsApps") })
            .SelectMany(DrivePaths.Subfolders);
}
