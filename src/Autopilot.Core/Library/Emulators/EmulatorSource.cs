using Autopilot.Core.Library.Stores;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Library.Emulators;

/// <summary>
/// The emulators on the machine, for a full scan only: every known emulator executable in the
/// usual places, each one an entry named <c>&lt;name&gt; (&lt;system&gt;)</c> with the executable
/// as the pick. Each folder once, the first executable found in it.
/// </summary>
public sealed class EmulatorSource(IRegistry registry, IDrives drives, HostFolders folders) : IGameSource
{
    public GameSource Source => GameSource.Emulator;

    public async Task<IReadOnlyList<StoreGame>> FindAsync(CancellationToken cancellationToken)
    {
        FoundGames found = new(GameSource.Emulator);
        foreach (EmulatorRoot root in await new EmulatorRoots(registry, drives, folders).ListAsync(cancellationToken))
        {
            foreach (string executable in EmulatorWalk.Executables(root.Folder, cancellationToken))
            {
                if (EmulatorProfiles.For(executable, genericNamesCount: root.Dedicated) is { } profile)
                {
                    found.Add(new(profile.Title, Path.GetDirectoryName(executable)!, GameSource.Emulator, executable));
                }
            }
        }

        return found.Games;
    }
}
