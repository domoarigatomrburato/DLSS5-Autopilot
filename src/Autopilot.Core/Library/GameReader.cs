using Autopilot.Core.Library.Stores;
using Autopilot.Core.Platform;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Library;

/// <summary>Reads one game: which executable it is, its architecture and graphics API, and what was installed.</summary>
public static class GameReader
{
    /// <summary>
    /// Reads the game a source found, in library.md's order: the executable and its candidates, the
    /// ranking over a store's stub and an earlier install's folder (unless the person picked the
    /// executable), the executable's tables, the graphics API, and the API the person chose. A
    /// folder with no executable, or one that cannot be read, gives an entry with the reason. The
    /// folders are listed on the calling thread, so call it from a background thread.
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

    private static async Task<GameEntry> DescribeAsync(
        StoreGame found, PickedExecutable pick, GameChoices? choices, GameReading context, List<LibraryNote> notes, CancellationToken cancellationToken)
    {
        GameEntry entry = new()
        {
            Name = found.Name,
            Folder = found.Folder,
            Source = found.Source,
            Executable = pick.Executable,
            Candidates = pick.Candidates,
            InstallFolder = pick.InstallFolder,
            Install = pick.Install,
        };
        if (pick.Executable is not { } executable)
        {
            notes.Add(new NoExecutableFound(found.Folder, found.Name));
            return entry with { Error = new GameReadError(GameReadFailure.NoExecutable, Texts.Text("GameNoExecutable")) };
        }

        return await ExecutableFacts.ReadAsync(entry, executable, choices, context.ApiSearch, cancellationToken);
    }
}
