using Autopilot.Core.Library.Emulators;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Platform;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Library;

/// <summary>Reads one game: which executable it is, its architecture and graphics API, and what was installed.</summary>
internal static class GameReader
{
    /// <summary>
    /// Reads the game a source found, in library.md's order: the executable and its candidates, the
    /// ranking over a store's stub and an earlier install's folder (unless the person picked the
    /// executable), the executable's tables, the graphics API, the API the person chose, and the
    /// emulator the executable is. A folder with no
    /// executable, or one that cannot be read, gives an entry with the reason. The folders are listed
    /// on the calling thread, so call it from a background thread.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task<GameRead> ReadAsync(StoreGame found, GameReading context, CancellationToken cancellationToken)
    {
        List<LibraryNote> notes = [];
        GameChoices? choices = context.Choices.ForFolder(found.Folder);
        string? picked = choices?.Executable is { } executable && DrivePaths.IsFile(executable) ? executable : null;
        PickedExecutable pick = await ExecutablePicker.PickAsync(found, picked, context.Records, notes, cancellationToken);
        return new GameRead(await DescribeAsync(found, pick, choices, context, notes, cancellationToken), notes);
    }

    /// <summary>Reads a game whose executable is already settled: the person's own pick.</summary>
    internal static async Task<GameRead> ReadPickedAsync(StoreGame found, string executable, GameReading context, CancellationToken cancellationToken)
    {
        List<LibraryNote> notes = [];
        PickedExecutable pick = await ExecutablePicker.PickedAsync(executable, [executable], context.Records, cancellationToken);
        return new GameRead(await DescribeAsync(found, pick, context.Choices.ForFolder(found.Folder), context, notes, cancellationToken), notes);
    }

    /// <summary>The game as its source listed it, rebuilt from its entry, so it can be read again as a scan reads it.</summary>
    internal static StoreGame Listing(GameEntry entry) => new(entry.Name, entry.Folder, entry.Source, entry.StoreExecutable);

    private static async Task<GameEntry> DescribeAsync(
        StoreGame found, PickedExecutable pick, GameChoices? choices, GameReading context, List<LibraryNote> notes, CancellationToken cancellationToken)
    {
        GameEntry entry = new()
        {
            Name = found.Name,
            Folder = found.Folder,
            Source = found.Source,
            Executable = pick.Executable,
            StoreExecutable = found.StoreExecutable,
            Candidates = pick.Candidates,
            InstallFolder = pick.InstallFolder,
            Install = pick.Install,
        };
        if (pick.Executable is not { } executable)
        {
            notes.Add(new NoExecutableFound(found.Folder, found.Name));
            return entry with { Error = new GameReadError(GameReadFailure.NoExecutable, Texts.Text("GameNoExecutable")) };
        }

        return await ExecutableFacts.ReadAsync(WithEmulator(entry, executable), executable, choices, context.ApiSearch, pick.Record, cancellationToken);
    }

    // Attached before the executable is read, so the API the emulator really draws with is decided in one go.
    private static GameEntry WithEmulator(GameEntry entry, string executable) => entry with { Emulator = EmulatorProfiles.For(executable, entry.Source) };
}
