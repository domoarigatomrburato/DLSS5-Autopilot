namespace Autopilot.Core.Platform;

/// <summary>A drive of the machine, by the root folder its paths start from (<c>D:\</c>).</summary>
public sealed record DriveRoot(string Root, DriveKind Kind)
{
    /// <summary>Whether the drive is in the machine (fixed or removable), not on the network.</summary>
    public bool IsLocal => Kind is DriveKind.Fixed or DriveKind.Removable;
}
