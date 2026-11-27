namespace Autopilot.Core.Storage;

/// <summary>What a drive named after a full-drive failure holds.</summary>
[Flags]
public enum DriveRoles
{
    None = 0,

    /// <summary>The write that found the drive full.</summary>
    FailedWrite = 1,

    /// <summary>The game's folder.</summary>
    Game = 2,

    /// <summary>The tool's download cache.</summary>
    Cache = 4,
}

/// <summary>One drive involved in a full-drive failure, with its free space and what it holds.</summary>
public sealed record DrivePlace(DriveSpace Drive, DriveRoles Roles);
