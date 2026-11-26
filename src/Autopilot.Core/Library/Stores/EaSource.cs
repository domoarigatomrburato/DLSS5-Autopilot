using Autopilot.Core.Platform;

namespace Autopilot.Core.Library.Stores;

/// <summary>
/// The EA app's and Origin's games as the registry and the default folders show them: every
/// <c>Electronic Arts\&lt;game&gt;</c> key's <c>Install Dir</c>, and every folder under
/// <c>Program Files\EA Games</c>. Named after the folder.
/// </summary>
public sealed class EaSource(IRegistry registry, HostFolders folders) : IGameSource
{
    private const string EaKey = @"SOFTWARE\Electronic Arts";

    private static readonly RegistryView[] _views = [RegistryView.Registry32, RegistryView.Registry64];

    public GameSource Source => GameSource.Ea;

    public Task<IReadOnlyList<StoreGame>> FindAsync(CancellationToken cancellationToken)
    {
        FoundGames found = new();
        foreach (string folder in RegistryFolders().Concat(DefaultFolders()))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DrivePaths.IsDirectory(folder))
            {
                found.Add(new(StoreFiles.FolderName(folder), folder, GameSource.Ea, null));
            }
        }

        return Task.FromResult(found.Games);
    }

    private IEnumerable<string> RegistryFolders() =>
        _views.SelectMany(view => registry.SubKeyNames(RegistryRoot.LocalMachine, view, EaKey)
            .Select(game => registry.ReadString(RegistryRoot.LocalMachine, view, $@"{EaKey}\{game}", "Install Dir"))
            .OfType<string>());

    private IEnumerable<string> DefaultFolders() =>
        new[] { folders.ProgramFiles, folders.ProgramFilesX86 }
            .OfType<string>()
            .SelectMany(programs => DrivePaths.Subfolders(Path.Join(programs, "EA Games")));
}
