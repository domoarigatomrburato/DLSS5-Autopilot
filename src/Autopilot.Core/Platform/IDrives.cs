namespace Autopilot.Core.Platform;

/// <summary>The machine's drives: one list every source searches, instead of a fixed set of letters.</summary>
public interface IDrives
{
    /// <summary>Every drive the machine reports, ready or not; a drive that is not ready is filtered by the reader, not here.</summary>
    IReadOnlyList<DriveRoot> List();
}
