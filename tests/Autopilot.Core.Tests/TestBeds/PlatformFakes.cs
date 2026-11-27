using Autopilot.Core.Platform;
using Autopilot.Core.Storage;

namespace Autopilot.Core.Tests.TestBeds;

/// <summary>Free space answering each path by the first folder it starts with.</summary>
internal sealed class FakeFreeSpace(params (string Folder, DriveSpace Drive)[] drives) : IFreeSpace
{
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
