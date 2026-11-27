namespace Autopilot.Core.Platform;

/// <summary>Whether the current user may write into a folder, asked without writing anything.</summary>
public interface IFolderAccess
{
    /// <summary>
    /// A read-only access check of <paramref name="folder"/> for the current user: never a probe
    /// file, since an install's refusal phase writes nothing.
    /// </summary>
    FolderAccess CanWrite(string folder);
}

/// <summary>What <see cref="IFolderAccess.CanWrite"/> found.</summary>
public enum FolderAccess
{
    /// <summary>The current user may create, change and delete files in the folder.</summary>
    Writable,

    /// <summary>The folder is there, and the current user may not write into it.</summary>
    Denied,

    /// <summary>There is no such folder.</summary>
    Missing,
}
