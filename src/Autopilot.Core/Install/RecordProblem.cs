namespace Autopilot.Core.Install;

/// <summary>Why a record that is there cannot be read: the reason behind <see cref="InstallState.Unreadable"/>.</summary>
public enum RecordProblem
{
    /// <summary>Not JSON, not an object, too large, or a folder where the file should be.</summary>
    Damaged,

    /// <summary>Parsable, but a required field is missing or of the wrong type.</summary>
    WrongShape,

    /// <summary>Written by a newer version of the tool: never edited.</summary>
    Newer,

    /// <summary>There, but it cannot be read right now (locked, denied): never taken for no record.</summary>
    Inaccessible,
}
