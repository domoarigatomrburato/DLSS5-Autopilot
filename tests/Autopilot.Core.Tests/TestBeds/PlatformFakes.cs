using Autopilot.Core.Platform;
using Autopilot.Core.Storage;

namespace Autopilot.Core.Tests.TestBeds;

/// <summary>Free space answering each path by the first folder it starts with.</summary>
internal sealed class FakeFreeSpace(params (string Folder, DriveSpace Drive)[] drives) : IFreeSpace
{
    /// <summary>One drive, G:, with 1 KB free, for every path: a transaction's, whose drives only a full-drive failure names.</summary>
    public static FakeFreeSpace OneDrive => new(("", new DriveSpace("G:", 1024)));

    public DriveSpace DriveOf(string path) =>
        drives.First(drive => path.StartsWith(drive.Folder, StringComparison.Ordinal)).Drive;
}

/// <summary>A process list the test fills.</summary>
internal sealed class FakeProcesses : IProcesses
{
    private readonly List<RunningProcess> _running = [];

    /// <summary>Starts a process from <paramref name="imagePath"/> (null: an image this user cannot read).</summary>
    public FakeProcesses Run(string? imagePath)
    {
        _running.Add(new(_running.Count + 100, imagePath));
        return this;
    }

    public IReadOnlyList<RunningProcess> List() => [.. _running];
}

/// <summary>Folder access as the test sets it per folder; otherwise writable when the folder exists, missing when not.</summary>
internal sealed class FakeFolderAccess : IFolderAccess
{
    private readonly Dictionary<string, FolderAccess> _set = [with(StringComparer.OrdinalIgnoreCase)];

    /// <summary>The current user may not write into <paramref name="folder"/> (an Xbox-like folder).</summary>
    public FakeFolderAccess Deny(string folder)
    {
        _set[Path.TrimEndingDirectorySeparator(folder)] = FolderAccess.Denied;
        return this;
    }

    public FolderAccess CanWrite(string folder) =>
        _set.TryGetValue(Path.TrimEndingDirectorySeparator(folder), out FolderAccess access) ? access
        : Directory.Exists(folder) ? FolderAccess.Writable
        : FolderAccess.Missing;
}

/// <summary>An elevation switch.</summary>
internal sealed class FakeElevation : IElevation
{
    public bool IsElevated { get; set; }
}

/// <summary>
/// The current user's registry in memory: keys and value names ignore case, as Windows does. A key or one value
/// can be made to refuse writes, and a key to refuse reads, with the exceptions Windows gives.
/// </summary>
internal sealed class FakeUserRegistry : IUserRegistry
{
    private readonly Dictionary<string, Dictionary<string, object>> _keys = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly HashSet<string> _refusingWrites = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly HashSet<string> _refusingReads = [with(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Sets a value as another program would, never refused.</summary>
    public FakeUserRegistry Set(string path, string name, object value)
    {
        KeyOf(path)[name] = value;
        return this;
    }

    /// <summary>Deletes a value as another program would.</summary>
    public FakeUserRegistry Remove(string path, string name)
    {
        _ = KeyOf(path).Remove(name);
        return this;
    }

    /// <summary>Writes to the key at <paramref name="path"/> (only to its value <paramref name="name"/>, when given) are denied.</summary>
    public FakeUserRegistry RefuseWrites(string path, string? name = null)
    {
        _ = _refusingWrites.Add(Id(path, name));
        return this;
    }

    /// <summary>Reads of the key at <paramref name="path"/> are denied.</summary>
    public FakeUserRegistry RefuseReads(string path)
    {
        _ = _refusingReads.Add(Id(path, null));
        return this;
    }

    /// <summary>Lets every write and read through again.</summary>
    public void Allow()
    {
        _refusingWrites.Clear();
        _refusingReads.Clear();
    }

    /// <summary>The value, as the test reads it; null when it is not there.</summary>
    public object? Value(string path, string name) => _keys.GetValueOrDefault(Id(path, null))?.GetValueOrDefault(name);

    /// <summary>Every value, as <c>key\name=value</c> lines sorted: what a test compares before and after.</summary>
    public string Snapshot() =>
        string.Join("\n", _keys.SelectMany(key => key.Value.Select(value => $"{key.Key}\\{value.Key}={value.Value}")).Order(StringComparer.OrdinalIgnoreCase));

    public IReadOnlyList<string> ValueNames(string path)
    {
        Readable(path);
        return _keys.TryGetValue(Id(path, null), out Dictionary<string, object>? values) ? [.. values.Keys] : [];
    }

    public int? ReadDword(string path, string name)
    {
        Readable(path);
        return Value(path, name) as int?;
    }

    public string? ReadString(string path, string name)
    {
        Readable(path);
        return Value(path, name) as string;
    }

    public void SetDword(string path, string name, int value) => Write(path, name, values => values[name] = value);

    public void SetString(string path, string name, string value) => Write(path, name, values => values[name] = value);

    public void DeleteValue(string path, string name) => Write(path, name, values => values.Remove(name));

    private void Write(string path, string name, Action<Dictionary<string, object>> change)
    {
        if (_refusingWrites.Contains(Id(path, null)) || _refusingWrites.Contains(Id(path, name)))
        {
            throw new UnauthorizedAccessException($@"Access to the registry key 'HKEY_CURRENT_USER\{path}' is denied.");
        }

        change(KeyOf(path));
    }

    private void Readable(string path)
    {
        if (_refusingReads.Contains(Id(path, null)))
        {
            throw new System.Security.SecurityException($@"Requested registry access is not allowed: HKEY_CURRENT_USER\{path}");
        }
    }

    private Dictionary<string, object> KeyOf(string path)
    {
        string id = Id(path, null);
        if (!_keys.TryGetValue(id, out Dictionary<string, object>? values))
        {
            values = [with(StringComparer.OrdinalIgnoreCase)];
            _keys[id] = values;
        }

        return values;
    }

    private static string Id(string path, string? name) => name is null ? path.Trim('\\') : $"{path.Trim('\\')}\n{name}";
}
