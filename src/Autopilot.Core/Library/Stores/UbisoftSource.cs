using Autopilot.Core.Platform;

namespace Autopilot.Core.Library.Stores;

/// <summary>
/// Ubisoft Connect's installed games, from <c>Ubisoft\Launcher\Installs\&lt;id&gt;</c>'s
/// <c>InstallDir</c>: the 32-bit view, else the 64-bit one. Named after the folder.
/// </summary>
public sealed class UbisoftSource(IRegistry registry) : IGameSource
{
    private const string InstallsKey = @"SOFTWARE\Ubisoft\Launcher\Installs";

    public GameSource Source => GameSource.Ubisoft;

    public Task<IReadOnlyList<StoreGame>> FindAsync(CancellationToken cancellationToken)
    {
        RegistryView view = registry.SubKeyNames(RegistryRoot.LocalMachine, RegistryView.Registry32, InstallsKey).Count > 0
            ? RegistryView.Registry32
            : RegistryView.Registry64;
        FoundGames found = new();
        foreach (string id in registry.SubKeyNames(RegistryRoot.LocalMachine, view, InstallsKey))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? folder = registry.ReadString(RegistryRoot.LocalMachine, view, $@"{InstallsKey}\{id}", "InstallDir");
            if (DrivePaths.IsDirectory(folder))
            {
                found.Add(new(StoreFiles.FolderName(folder!), folder!, GameSource.Ubisoft, null));
            }
        }

        return Task.FromResult(found.Games);
    }
}
