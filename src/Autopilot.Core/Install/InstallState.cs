namespace Autopilot.Core.Install;

/// <summary>What the install record in a folder says, as install's reader answers for it.</summary>
public enum InstallState
{
    /// <summary>An install started and did not finish: the game shows as installed, and offers to finish it.</summary>
    Installing,

    /// <summary>The install finished.</summary>
    Installed,

    /// <summary>An uninstall started and did not finish.</summary>
    Removing,

    /// <summary>A record is there but cannot be read: damaged, the wrong shape, or a newer format.</summary>
    Unreadable,
}
