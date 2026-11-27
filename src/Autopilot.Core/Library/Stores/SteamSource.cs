using Autopilot.Core.Platform;

namespace Autopilot.Core.Library.Stores;

/// <summary>
/// Steam's installed games: the folders under every library's <c>steamapps\common</c> that a manifest
/// says are fully installed, or that hold an executable, or that hold an install record of ours.
/// </summary>
public sealed class SteamSource(IRegistry registry, HostFolders folders) : IGameSource
{
    public GameSource Source => GameSource.Steam;

    public async Task<IReadOnlyList<StoreGame>> FindAsync(CancellationToken cancellationToken)
    {
        string? root = SteamLibraries.Root(registry, folders);
        if (root is null)
        {
            return [];
        }

        FoundGames found = new();
        foreach (string library in await SteamLibraries.ListAsync(root, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await AddLibraryAsync(library, found, cancellationToken);
        }

        return found.Games;
    }

    private static async Task AddLibraryAsync(string library, FoundGames found, CancellationToken cancellationToken)
    {
        string steamapps = Path.Join(library, "steamapps");
        Dictionary<string, SteamManifest> manifests = ByFolder(await SteamManifest.ReadAllAsync(steamapps, cancellationToken));
        foreach (string folder in DrivePaths.Subfolders(Path.Join(steamapps, "common")))
        {
            string name = Path.GetFileName(folder);
            SteamManifest? manifest = manifests.GetValueOrDefault(name);
            if (SteamFolder.Counts(folder, manifest))
            {
                found.Add(new(manifest?.Name ?? name, folder, GameSource.Steam, null));
            }
        }
    }

    // Two manifests naming one folder: a fully installed one says more than one still downloading.
    private static Dictionary<string, SteamManifest> ByFolder(List<SteamManifest> manifests)
    {
        Dictionary<string, SteamManifest> byFolder = [with(StringComparer.OrdinalIgnoreCase)];
        foreach (SteamManifest manifest in manifests)
        {
            if (!byFolder.TryGetValue(manifest.InstallDir, out SteamManifest? known) || (!known.FullyInstalled && manifest.FullyInstalled))
            {
                byFolder[manifest.InstallDir] = manifest;
            }
        }

        return byFolder;
    }
}
