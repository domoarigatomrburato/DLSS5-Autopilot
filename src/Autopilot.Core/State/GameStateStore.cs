using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Autopilot.Core.Platform;
using Autopilot.Core.Resources;

namespace Autopilot.Core.State;

/// <summary>
/// What the tool learned about each game (the last verdicts, the autopilot plan, the "aim for"
/// history, an install's failure note), one file per game: <c>&lt;root&gt;\games\&lt;key&gt;.json</c>,
/// keyed by <see cref="GameIdentity.Key"/>. Every file carries <c>"schema": 1</c> and its game's
/// <c>"folder"</c>, so the store can list every folder it knows.
/// </summary>
/// <remarks>
/// One writer: a process makes one instance and shares it, and across processes (the app and the
/// CLI) each file is written under a named mutex, <c>Local\invidia-claritatis-games-&lt;key&gt;</c>,
/// re-read inside it. The calls are synchronous (the files are small, and the mutex belongs to the
/// thread that took it), and <see cref="Update"/> and <see cref="Drop"/> block while another writer
/// holds the file: call them off the UI thread.
/// </remarks>
public sealed class GameStateStore
{
    private const int Schema = 1;
    private const string Extension = ".json";

    private readonly string _folder;
    private readonly TimeProvider _time;
    private readonly Func<Stream, Stream> _through;
    private readonly Func<string, Mutex> _openMutex;
    private readonly ConcurrentDictionary<GameIdentity, StoreFile> _files = new();
    private readonly ConcurrentQueue<StoreNotice> _notices = new();

    /// <param name="root">The tool's data folder, <c>%LOCALAPPDATA%\invidia-claritatis</c>.</param>
    /// <param name="time">Times the waits between reads of a locked file.</param>
    public GameStateStore(string root, TimeProvider time)
        : this(root, time, file => file, NamedMutex.Open)
    {
    }

    /// <summary>
    /// The store, with every write's stream wrapped by <paramref name="through"/> (the full-drive tests
    /// fill the drive through it) and each game's mutex opened by <paramref name="openMutex"/> (the tests
    /// refuse it, as an elevated process's mutex refuses the app).
    /// </summary>
    internal GameStateStore(string root, TimeProvider time, Func<Stream, Stream> through, Func<string, Mutex> openMutex)
    {
        _folder = Path.Join(root, "games");
        _time = time;
        _through = through;
        _openMutex = openMutex;
    }

    /// <summary>A copy of the game's state: empty defaults when the store has none, never an exception.</summary>
    public GameState Read(GameIdentity game) => new(FileOf(game).Read());

    /// <summary>
    /// Applies <paramref name="change"/> to the game's state as the file holds it now and writes it.
    /// When it cannot be written (a newer version's file, a locked file, a full drive) the change is
    /// kept in memory for this process, a notice is queued, and the caller gets no exception.
    /// </summary>
    /// <param name="game">The game.</param>
    /// <param name="change">Changes the state.</param>
    /// <param name="cancellationToken">Stops the wait for another writer, and the write.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public void Update(GameIdentity game, Action<GameState> change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        FileOf(game).Update(document => change(new GameState(document)), cancellationToken);
    }

    /// <summary>
    /// Forgets the game: its file is deleted (a full uninstall). A newer version's file is left in
    /// place. When the file cannot be deleted, the game reads as dropped for this process (and is not
    /// listed) until a later drop deletes it or a later change is written over it, and a notice is queued.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was cancelled while waiting for another writer.</exception>
    public void Drop(GameIdentity game, CancellationToken cancellationToken) => FileOf(game).Delete(cancellationToken);

    /// <summary>The folder of every game the store holds state for, on disk or kept in memory, sorted.</summary>
    public IReadOnlyList<string> Folders()
    {
        SortedSet<string> folders = [with(StringComparer.OrdinalIgnoreCase)];
        foreach (string file in DrivePaths.Files(_folder).Where(file => file.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)))
        {
            if (StoreFile.Peek(file, GameState.SchemaKey, Schema)?[GameState.FolderKey] is JsonValue value && value.TryGetValue(out string? folder) && folder.Length > 0)
            {
                _ = folders.Add(folder);
            }
        }

        folders.UnionWith(_files.Where(file => file.Value.HasUnsaved).Select(file => file.Key.Folder));
        folders.ExceptWith(_files.Where(file => file.Value.DropPending).Select(file => file.Key.Folder));

        return [.. folders];
    }

    /// <summary>The notices queued since the last call, oldest first; each kind is queued once per game per process.</summary>
    public IReadOnlyList<StoreNotice> TakeNotices()
    {
        List<StoreNotice> taken = [];
        while (_notices.TryDequeue(out StoreNotice? notice))
        {
            taken.Add(notice);
        }

        return taken;
    }

    private StoreFile FileOf(GameIdentity game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return _files.GetOrAdd(game, identity => new(
            Path.Join(_folder, identity.Key + Extension),
            @"Local\invidia-claritatis-games-" + identity.Key,
            _openMutex,
            GameState.SchemaKey,
            Schema,
            document => document[GameState.FolderKey] = identity.Folder,
            _time,
            kind => _notices.Enqueue(new(kind, identity.Folder, Explain(kind, identity.Folder, identity.Key))),
            _through));
    }

    private static string Explain(StoreNoticeKind kind, string folder, string key) => kind switch
    {
        StoreNoticeKind.NewerVersion => Texts.Format("GameStateNewer", folder),
        StoreNoticeKind.Damaged => Texts.Format("GameStateDamaged", folder, $"{key}{Extension}.corrupt"),
        StoreNoticeKind.Unreadable => Texts.Format("GameStateUnreadable", folder),
        StoreNoticeKind.DiskFull => Texts.Format("GameStateDiskFull", folder),
        _ => Texts.Format("GameStateNotSaved", folder),
    };
}
