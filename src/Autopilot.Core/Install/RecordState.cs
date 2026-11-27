namespace Autopilot.Core.Install;

/// <summary>The install record's own lifecycle state, as written in the file.</summary>
public enum RecordState
{
    /// <summary>A transaction is open: entries are pending or written.</summary>
    Installing,

    /// <summary>The install committed.</summary>
    Installed,

    /// <summary>An uninstall or a rollback is part way, or ended with leftovers.</summary>
    Removing,
}
