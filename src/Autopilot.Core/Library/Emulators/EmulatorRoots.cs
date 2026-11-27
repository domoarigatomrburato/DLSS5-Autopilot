using Autopilot.Core.Library.Stores;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Library.Emulators;

/// <summary>Where the emulator search looks, each resolved folder once.</summary>
internal sealed class EmulatorRoots(IRegistry registry, IDrives drives, HostFolders folders)
{
    // The folders people keep emulators in, at a drive's root.
    private static readonly string[] _driveFolders =
    [
        "Emulators", "Emulator", "Games", "Emu", "RetroArch", "PS2", "PS1", "Emulation", "Roms", "Apps", "Programs", "Oyunlar", "Tools", "Portable", "EmuDeck",
        "Emulators_Portable", "LaunchBox",
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
    /// folder. Only directories, each resolved folder once.
    /// </summary>
    public async Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken)
    {
        IEnumerable<string?> candidates = Registered()
            .Concat([folders.ProgramFiles, folders.ProgramFilesX86, folders.LocalAppData, folders.AppData, folders.Desktop, folders.Downloads, folders.Documents])
            .Concat(OnDrives())
            .Concat(await SteamLibrariesAsync(cancellationToken));
        HashSet<string> seen = [with(StringComparer.OrdinalIgnoreCase)];
        return [.. candidates.OfType<string>().Where(root => DrivePaths.IsDirectory(root) && seen.Add(ResolvedPath.Of(root)))];
    }

    private IEnumerable<string?> Registered() =>
        UninstallEntry.ReadAll(registry).Where(entry => entry.DisplayName is { } name && _words.Any(word => HasWord(name, word))).Select(entry => entry.InstallLocation);

    // People unpack an emulator straight to D:\PCSX2, so a drive's root is searched too; the system
    // drive's root holds Windows itself and is left to Program Files.
    private IEnumerable<string> OnDrives() =>
        drives.List()
            .Where(drive => drive.Kind is DriveKind.Fixed or DriveKind.Removable)
            .SelectMany(drive => _driveFolders.Select(name => Path.Join(drive.Root, name)).Concat(IsSystemDrive(drive) ? [] : [drive.Root]));

    // Program Files is always on the system drive; when it is not known, no drive's root is searched.
    private bool IsSystemDrive(DriveRoot drive) =>
        folders.ProgramFiles is not { } programs || programs.StartsWith(drive.Root, StringComparison.OrdinalIgnoreCase);

    // RetroArch, DuckStation and Dolphin ship on Steam, and a Steam library is rarely anywhere above.
    private async Task<IEnumerable<string>> SteamLibrariesAsync(CancellationToken cancellationToken) =>
        SteamLibraries.Root(registry, folders) is { } steam
            ? (await SteamLibraries.ListAsync(steam, cancellationToken)).Select(library => Path.Join(library, "steamapps", "common"))
            : [];

    // A whole word, any case: "Dolphin Emulator" names Dolphin, "Playground" does not name Play!.
    private static bool HasWord(string text, string word)
    {
        for (int at = text.IndexOf(word, StringComparison.OrdinalIgnoreCase); at >= 0; at = text.IndexOf(word, at + 1, StringComparison.OrdinalIgnoreCase))
        {
            if (!IsWordCharacter(text, at - 1) && !IsWordCharacter(text, at + word.Length))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWordCharacter(string text, int index) => index >= 0 && index < text.Length && char.IsLetterOrDigit(text[index]);
}
