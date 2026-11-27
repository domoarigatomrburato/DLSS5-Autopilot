using System.Text.Json.Nodes;
using Autopilot.Core.Graphics;
using Autopilot.Core.Platform;
using Autopilot.Core.Storage;

namespace Autopilot.Core.State;

/// <summary>
/// One JSON store file and its rules, whatever it holds (the per-game state now, the settings in M7):
/// a version key, unknown keys kept, one writer across processes under a named mutex, re-reading
/// inside it, atomic writes, and what happens when the file is newer, damaged, locked or the drive
/// full. What a change could not write is kept as an overlay (the top-level keys it set, with their
/// values, and the keys it removed) and laid over every later read in this process, so nothing
/// another process wrote is lost and nothing this one asked for is dropped.
/// </summary>
/// <param name="path">The file.</param>
/// <param name="mutexName">The cross-process mutex guarding the file, the same in every process.</param>
/// <param name="openMutex">Opens that mutex; one that cannot be opened is a file that cannot be written now.</param>
/// <param name="versionKey">The key the version is written under.</param>
/// <param name="version">The version this build writes; a file with a higher one is never written.</param>
/// <param name="stamp">Sets what the store writes itself besides the version, before every write.</param>
/// <param name="time">Times the waits between reads of a locked file.</param>
/// <param name="notify">Called once per kind for this file, the first time it happens.</param>
/// <param name="through">Wraps the temporary file's stream as it is written; tests fill the drive through it.</param>
internal sealed class StoreFile(
    string path,
    string mutexName,
    Func<string, Mutex> openMutex,
    string versionKey,
    int version,
    Action<JsonObject> stamp,
    TimeProvider time,
    Action<StoreNoticeKind> notify,
    Func<Stream, Stream> through)
{
    /// <summary>A store file is a few KiB; anything past this was not written by the tool.</summary>
    public const long MaxBytes = 1024 * 1024;

    // A file locked by an antivirus or an indexer is usually free again within a fraction of a second.
    private const int ReadTries = 3;
    private static readonly TimeSpan _retryWait = TimeSpan.FromMilliseconds(100);

    private readonly Lock _lock = new();
    private readonly Dictionary<string, JsonNode?> _set = [with(StringComparer.Ordinal)];
    private readonly HashSet<string> _removed = [with(StringComparer.Ordinal)];
    private readonly HashSet<StoreNoticeKind> _noticed = [];
    private readonly HashSet<string> _ownKeys = OwnKeys(versionKey, stamp);
    private JsonObject _lastGood = [];

    /// <summary>Whether a change asked for in this process is not in the file.</summary>
    public bool HasUnsaved
    {
        get
        {
            lock (_lock)
            {
                return _set.Count > 0 || _removed.Count > 0;
            }
        }
    }

    /// <summary>Whether this process dropped the file but could not delete it, and nothing was set since; changed under the lock.</summary>
    public bool DropPending { get; private set; }

    /// <summary>
    /// The document: the file as it is now, with this process's unsaved changes laid over it. A
    /// damaged file is moved aside when no one is writing it (otherwise its writer does that).
    /// </summary>
    public JsonObject Read()
    {
        // A read waits only through the few short retries of a locked file, so it is not cancellable.
        Loaded loaded = Load(CancellationToken.None);
        if (loaded.State == JsonFileState.Damaged)
        {
            SetAsideIfFree();
        }

        return WithOverlay(loaded.Document);
    }

    /// <summary>
    /// Under the cross-process lock, re-reads the file, applies <paramref name="change"/> on top of
    /// any unsaved change and writes the result atomically. A file that is newer or could not be read
    /// is not written, nor is one whose mutex cannot be opened; a write that fails keeps the change in
    /// memory. Either way a notice is queued, and the caller gets no exception but cancellation (nothing
    /// is kept then) and the change's own. Blocks: call it off the UI thread.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was cancelled while waiting for the lock or writing.</exception>
    public void Update(Action<JsonObject> change, CancellationToken cancellationToken)
    {
        using NamedMutex? held = Hold(cancellationToken);
        Loaded loaded = Load(cancellationToken);
        // A damaged file that cannot be moved aside is not written over: it would be lost.
        bool writable = held is not null && loaded.State switch
        {
            JsonFileState.Newer or JsonFileState.Unreadable => false,
            JsonFileState.Damaged => SetAside(),
            _ => true,
        };
        JsonObject before = WithOverlay(loaded.Document);
        var next = (JsonObject)before.DeepClone();
        change(next);
        if (writable && Write(next, cancellationToken))
        {
            lock (_lock)
            {
                Forget();
                _lastGood = next;
            }

            return;
        }

        Keep(before, next);
    }

    /// <summary>
    /// Deletes the file under the cross-process lock and forgets what this process kept. A file from
    /// a newer version is left in place, like any write to it; a damaged one is moved aside. A file
    /// that cannot be deleted (or its mutex opened) reads as dropped for this process: its keys are
    /// kept as removed, laid over every read until a delete or a write succeeds. Blocks: call it off
    /// the UI thread.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was cancelled while waiting for the lock.</exception>
    public void Delete(CancellationToken cancellationToken)
    {
        using NamedMutex? held = Hold(cancellationToken);
        Loaded loaded = Load(cancellationToken);
        bool gone = held is not null && loaded.State switch
        {
            JsonFileState.Damaged => SetAside(),
            JsonFileState.Missing or JsonFileState.Newer => true,
            _ => DeleteFile(),
        };
        lock (_lock)
        {
            if (gone)
            {
                Forget();
                _lastGood = [];
                return;
            }

            // The drop, kept: every key the file and this process hold, but the store's own.
            _removed.UnionWith(loaded.Document.Select(pair => pair.Key).Concat(_set.Keys).Where(key => !_ownKeys.Contains(key)));
            _set.Clear();
            DropPending = true;
        }
    }

    /// <summary>The object a store file holds, or null when it is missing, too large, unreadable or damaged; reads once, says nothing.</summary>
    public static JsonObject? Peek(string path, string versionKey, int version) => VersionedJson.Read(path, MaxBytes, versionKey, version).Document;

    // Callers hold _lock.
    private void Forget()
    {
        _set.Clear();
        _removed.Clear();
        DropPending = false;
    }

    private bool DeleteFile()
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Notice(StoreNoticeKind.NotSaved);
            return false;
        }
    }

    // An elevated process can create the mutex with an access list that refuses this one: then the
    // file cannot be written now, which is said like any other write that failed.
    private NamedMutex? Hold(CancellationToken cancellationToken)
    {
        try
        {
            return NamedMutex.Hold(mutexName, openMutex, cancellationToken);
        }
        catch (Exception e) when (IsRefused(e))
        {
            Notice(StoreNoticeKind.NotSaved);
            return null;
        }
    }

    private static bool IsRefused(Exception e) => e is UnauthorizedAccessException or WaitHandleCannotBeOpenedException or IOException;

    private static HashSet<string> OwnKeys(string versionKey, Action<JsonObject> stamp)
    {
        JsonObject stamped = [];
        stamp(stamped);
        return [with(StringComparer.Ordinal), versionKey, .. stamped.Select(pair => pair.Key)];
    }

    private Loaded Load(CancellationToken cancellationToken)
    {
        VersionedRead read = VersionedJson.Read(path, MaxBytes, versionKey, version);
        for (int tries = 1; read.State == JsonFileState.Unreadable && tries < ReadTries; tries++)
        {
            Task.Delay(_retryWait, time, cancellationToken).GetAwaiter().GetResult();
            read = VersionedJson.Read(path, MaxBytes, versionKey, version);
        }

        Notice(read.State);
        lock (_lock)
        {
            if (read.State == JsonFileState.Unreadable)
            {
                return new(read.State, (JsonObject)_lastGood.DeepClone());
            }

            JsonObject document = read.Document ?? [];
            _lastGood = (JsonObject)document.DeepClone();
            return new(read.State, document);
        }
    }

    private void Notice(JsonFileState state)
    {
        switch (state)
        {
            case JsonFileState.Unreadable:
                Notice(StoreNoticeKind.Unreadable);
                break;
            case JsonFileState.Damaged:
                Notice(StoreNoticeKind.Damaged);
                break;
            case JsonFileState.Newer:
                Notice(StoreNoticeKind.NewerVersion);
                break;
        }
    }

    private JsonObject WithOverlay(JsonObject document)
    {
        lock (_lock)
        {
            foreach (string key in _removed)
            {
                _ = document.Remove(key);
            }

            foreach ((string key, JsonNode? value) in _set)
            {
                document[key] = value?.DeepClone();
            }
        }

        return document;
    }

    // What the change did, by top-level key, laid over every later read until a write carries it.
    private void Keep(JsonObject before, JsonObject after)
    {
        lock (_lock)
        {
            foreach ((string key, JsonNode? value) in after)
            {
                if (!before.TryGetPropertyValue(key, out JsonNode? old) || !JsonNode.DeepEquals(old, value))
                {
                    _set[key] = value?.DeepClone();
                    _ = _removed.Remove(key);
                    DropPending = false;
                }
            }

            foreach (string key in before.Select(pair => pair.Key).Where(key => !after.ContainsKey(key)))
            {
                _ = _set.Remove(key);
                _ = _removed.Add(key);
            }
        }
    }

    private bool Write(JsonObject document, CancellationToken cancellationToken)
    {
        var written = (JsonObject)document.DeepClone();
        written[versionKey] = version;
        stamp(written);
        try
        {
            StoreWrite.Whole(path, written, through, time, cancellationToken);
            return true;
        }
        catch (Exception e) when (IoErrors.IsDiskFull(e))
        {
            Notice(StoreNoticeKind.DiskFull);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Notice(StoreNoticeKind.NotSaved);
        }

        return false;
    }

    // Moved aside, the newest damaged copy kept, so the person can still look at it; never deleted.
    private bool SetAside()
    {
        // A folder where the file should be is left alone: nothing of the tool's is in it.
        if (Directory.Exists(path))
        {
            Notice(StoreNoticeKind.NotSaved);
            return false;
        }

        try
        {
            File.Move(path, path + KnownFiles.CorruptSuffix, overwrite: true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Tried again by the next write.
            Notice(StoreNoticeKind.NotSaved);
            return false;
        }
    }

    private void SetAsideIfFree()
    {
        using NamedMutex? held = TryHold();
        if (held is not null && VersionedJson.Read(path, MaxBytes, versionKey, version).State == JsonFileState.Damaged)
        {
            _ = SetAside();
        }
    }

    // A mutex that cannot be opened is not free.
    private NamedMutex? TryHold()
    {
        try
        {
            return NamedMutex.TryHold(mutexName, openMutex);
        }
        catch (Exception e) when (IsRefused(e))
        {
            return null;
        }
    }

    private void Notice(StoreNoticeKind kind)
    {
        bool first;
        lock (_lock)
        {
            first = _noticed.Add(kind);
        }

        if (first)
        {
            notify(kind);
        }
    }

    private sealed record Loaded(JsonFileState State, JsonObject Document);
}
