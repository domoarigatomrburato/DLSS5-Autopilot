using Autopilot.Core.Library.Stores;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Library.Emulators;

/// <summary>Where the emulator search looks, each resolved folder once.</summary>
internal sealed class EmulatorRoots(IRegistry registry, IDrives drives, HostFolders folders)
{
    // The folders people keep emulators in, at a drive's root; the ones also holding games and other programs are not dedicated.
    private static readonly (string Name, bool Dedicated)[] _driveFolders =
    [
        ("Emulators", true), ("Emulator", true), ("Games", false), ("Emu", true), ("RetroArch", true), ("PS2", true), ("PS1", true), ("Emulation", true),
        ("Roms", true), ("Apps", false), ("Programs", false), ("Oyunlar", false), ("Tools", false), ("Portable", false), ("EmuDeck", true),
        ("Emulators_Portable", true), ("LaunchBox", true),
    ];

    // Each profile's executable stems and each of its names: "yuzu / suyu / Eden / Citron" is four, "Play!" is "Play".
    private static readonly string[] _words =
    [
        .. EmulatorProfiles.All
            .SelectMany(profile => profile.Name.Split([" / ", "!"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Concat(profile.Executables.Select(Path.GetFileNameWithoutExtension).OfType<string>()))
            .Distinct(StringComparer.OrdinalIgnoreCase),
    ];

    /// <summary>
    /// The install folders of the Add/Remove Programs entries named after an emulator, the host's
    /// program, application data and personal folders, the usual folders on every fixed or removable
    /// drive, each such drive's root except the system drive's, and every Steam library's games
    /// folder. Only directories, each resolved folder once, dedicated when any way it was reached is.
    /// </summary>
    public async Task<IReadOnlyList<EmulatorRoot>> ListAsync(CancellationToken cancellationToken)
    {
        IEnumerable<(string? Folder, bool Dedicated)> candidates = Registered().Select(folder => (folder, true))
            .Concat(new[] { folders.ProgramFiles, folders.ProgramFilesX86, folders.LocalAppData, folders.AppData, folders.Desktop, folders.Downloads, folders.Documents }.Select(folder => (folder, false)))
            .Concat(OnDrives())
            .Concat((await SteamLibrariesAsync(cancellationToken)).Select(folder => ((string?)folder, false)));
        List<EmulatorRoot> roots = [];
        Dictionary<string, int> at = [with(StringComparer.OrdinalIgnoreCase)];
        foreach ((string? folder, bool dedicated) in candidates)
        {
            if (!DrivePaths.IsDirectory(folder))
            {
                continue;
            }

            string resolved = ResolvedPath.Of(folder!);
            if (!at.TryGetValue(resolved, out int index))
            {
                at[resolved] = roots.Count;
                roots.Add(new EmulatorRoot(folder!, dedicated));
            }
            else if (dedicated)
            {
                roots[index] = roots[index] with { Dedicated = true };
            }
        }

        return roots;
    }

    private IEnumerable<string?> Registered() =>
        UninstallEntry.ReadAll(registry).Where(entry => entry.DisplayName is { } name && _words.Any(word => WholeWord.In(name, word))).Select(entry => entry.InstallLocation);

    // People unpack an emulator straight to D:\PCSX2, so a drive's root is searched too; the system
    // drive's root holds Windows itself and is left to Program Files.
    private IEnumerable<(string? Folder, bool Dedicated)> OnDrives() =>
        drives.List()
            .Where(drive => drive.IsLocal)
            .SelectMany(drive => _driveFolders
                .Select(folder => ((string?)Path.Join(drive.Root, folder.Name), folder.Dedicated))
                .Concat(IsSystemDrive(drive) ? [] : [(drive.Root, false)]));

    // Program Files is always on the system drive; when it is not known, no drive's root is searched.
    private bool IsSystemDrive(DriveRoot drive) =>
        folders.ProgramFiles is not { } programs || programs.StartsWith(drive.Root, StringComparison.OrdinalIgnoreCase);

    // RetroArch, DuckStation and Dolphin ship on Steam, and a Steam library is rarely anywhere above.
    private async Task<IEnumerable<string>> SteamLibrariesAsync(CancellationToken cancellationToken) =>
        SteamLibraries.Root(registry, folders) is { } steam
            ? (await SteamLibraries.ListAsync(steam, cancellationToken)).Select(library => Path.Join(library, "steamapps", "common"))
            : [];
}
