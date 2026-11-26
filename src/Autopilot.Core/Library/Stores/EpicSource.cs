using System.Text.Json;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Library.Stores;

/// <summary>
/// The Epic Games Launcher's installed games, from its <c>.item</c> manifests under
/// <c>%PROGRAMDATA%\Epic\EpicGamesLauncher\Data\Manifests</c>. A folder several manifests share
/// (a game and its DLC) is named after the manifest of the main game.
/// </summary>
public sealed class EpicSource(HostFolders folders) : IGameSource
{
    public GameSource Source => GameSource.Epic;

    public async Task<IReadOnlyList<StoreGame>> FindAsync(CancellationToken cancellationToken)
    {
        if (folders.ProgramData is not { } programData)
        {
            return [];
        }

        string epic = Path.Join(programData, "Epic");
        List<EpicManifest> manifests = await ManifestsAsync(Path.Join(epic, "EpicGamesLauncher", "Data", "Manifests"), cancellationToken);
        Dictionary<string, string> launcherList = await LauncherInstalledAsync(Path.Join(epic, "UnrealEngineLauncher", "LauncherInstalled.dat"), cancellationToken);

        FoundGames found = new();
        foreach ((EpicManifest manifest, string? folder) in manifests
            .Select(manifest => (manifest, folder: Location(manifest, launcherList)))
            .Where(pair => pair.folder is not null)
            .OrderByDescending(pair => pair.manifest.IsMainGame))
        {
            found.Add(new(manifest.DisplayName ?? StoreFiles.FolderName(folder!), folder!, GameSource.Epic, StoreFiles.ExistingExecutable(folder!, manifest.LaunchExecutable)));
        }

        return found.Games;
    }

    // A manifest's location can be stale after a game was moved; the launcher's own list then has the right one.
    private static string? Location(EpicManifest manifest, Dictionary<string, string> launcherList)
    {
        if (DrivePaths.IsDirectory(manifest.InstallLocation))
        {
            return manifest.InstallLocation;
        }

        return launcherList.TryGetValue(manifest.AppName, out string? listed) && DrivePaths.IsDirectory(listed) ? listed : null;
    }

    private static async Task<List<EpicManifest>> ManifestsAsync(string folder, CancellationToken cancellationToken)
    {
        List<EpicManifest> manifests = [];
        foreach (string file in DrivePaths.Files(folder).Where(file => file.EndsWith(".item", StringComparison.OrdinalIgnoreCase)))
        {
            using JsonDocument? item = await StoreFiles.ReadJsonAsync(file, cancellationToken);
            if (item is not null && EpicManifest.From(item.RootElement) is { } manifest)
            {
                manifests.Add(manifest);
            }
        }

        return manifests;
    }

    // InstallationList: [{ "AppName": ..., "InstallLocation": ... }], by app name.
    private static async Task<Dictionary<string, string>> LauncherInstalledAsync(string path, CancellationToken cancellationToken)
    {
        Dictionary<string, string> locations = [with(StringComparer.OrdinalIgnoreCase)];
        using JsonDocument? list = await StoreFiles.ReadJsonAsync(path, cancellationToken);
        if (list is null || list.RootElement.ValueKind != JsonValueKind.Object
            || !list.RootElement.TryGetProperty("InstallationList", out JsonElement apps) || apps.ValueKind != JsonValueKind.Array)
        {
            return locations;
        }

        foreach (JsonElement app in apps.EnumerateArray())
        {
            if (StoreFiles.Text(app, "AppName") is { Length: > 0 } name && StoreFiles.Text(app, "InstallLocation") is { } location)
            {
                _ = locations.TryAdd(name, location);
            }
        }

        return locations;
    }
}
