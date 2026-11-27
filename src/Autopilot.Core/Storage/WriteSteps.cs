namespace Autopilot.Core.Storage;

/// <summary>
/// Told before every change an install or an uninstall makes on disk, so a test can stop it there:
/// a crash at any step, a full drive at any step. Production uses <see cref="NoWriteSteps"/>.
/// </summary>
internal interface IWriteSteps
{
    /// <summary>Called just before <paramref name="step"/> is made; an exception stops it.</summary>
    void Before(WriteStep step);
}

/// <summary>One change on disk: what kind, the path it changes, and where a move or a copy puts it.</summary>
/// <param name="Kind">What the change does.</param>
/// <param name="Path">The file or folder changed: the source of a move or a copy.</param>
/// <param name="Destination">Where a <see cref="WriteKind.Move"/> or a <see cref="WriteKind.Copy"/> puts it; null for the other kinds.</param>
internal sealed record WriteStep(WriteKind Kind, string Path, string? Destination = null);

/// <summary>The kinds of change an install or an uninstall makes on disk.</summary>
internal enum WriteKind
{
    /// <summary>A new file written.</summary>
    Create,

    /// <summary>An existing file replaced.</summary>
    Replace,

    /// <summary>A file moved or renamed (a backup, a moved-aside file).</summary>
    Move,

    /// <summary>A file copied (a backup by copy).</summary>
    Copy,

    /// <summary>A file deleted.</summary>
    Delete,

    /// <summary>A file's attributes changed (read-only cleared or restored).</summary>
    SetAttributes,

    /// <summary>A folder created.</summary>
    CreateFolder,

    /// <summary>The install record saved.</summary>
    SaveRecord,

    /// <summary>A value of the current user's registry set; the path is the key, then the value's name.</summary>
    SetRegistryValue,

    /// <summary>A value of the current user's registry deleted; the path is the key, then the value's name.</summary>
    DeleteRegistryValue,
}

/// <summary>The production <see cref="IWriteSteps"/>: every step goes ahead.</summary>
internal sealed class NoWriteSteps : IWriteSteps
{
    public static readonly NoWriteSteps Instance = new();

    private NoWriteSteps()
    {
    }

    public void Before(WriteStep step)
    {
        // Nothing to stop.
    }
}
