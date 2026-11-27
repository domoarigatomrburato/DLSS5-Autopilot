using Autopilot.Core.Storage;

namespace Autopilot.Core.Platform;

/// <summary>
/// Which files another program holds open right now (a launcher holding the proxy DLL, a game that
/// loaded one), so an install names them before it writes anything instead of ending half-written.
/// A read-only attribute is not a lock: the tool clears it on its own files, and backs up the game's
/// by rename, which works on a read-only file.
/// </summary>
internal static class FileLocks
{
    /// <summary>
    /// The files of <paramref name="paths"/> that cannot be opened alone now. Each is opened for
    /// writing with no sharing and closed at once (open, never create or truncate: nothing changes);
    /// a file with the read-only attribute is opened for reading instead, so the attribute alone is not
    /// a lock. A missing file is not listed.
    /// </summary>
    /// <remarks>
    /// The write open is what finds a DLL another process loaded: once the loader has closed its
    /// handle, the mapped image still refuses writes on Windows while a read open with no sharing
    /// succeeds. Checked on Windows in M7.
    /// </remarks>
    public static IReadOnlyList<HeldFile> Held(IEnumerable<string> paths) =>
        [.. paths.Select(path => Reason(path) is { } reason ? new HeldFile(path, reason) : null).OfType<HeldFile>()];

    private static HeldReason? Reason(string path)
    {
        try
        {
            FileAccess access = File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly) ? FileAccess.Read : FileAccess.Write;
            using FileStream _ = new(path, FileMode.Open, access, FileShare.None);
            return null;
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (IOException e) when (IoErrors.IsSharingViolation(e))
        {
            return HeldReason.InUse;
        }
        catch (UnauthorizedAccessException)
        {
            return HeldReason.Denied;
        }
    }
}

/// <summary>A file an install cannot change now, and why.</summary>
internal sealed record HeldFile(string Path, HeldReason Reason);

/// <summary>Why a file is held.</summary>
internal enum HeldReason
{
    /// <summary>Another program has it open.</summary>
    InUse,

    /// <summary>The current user may not open it: not even for reading when it is read-only, not for writing otherwise.</summary>
    Denied,
}
