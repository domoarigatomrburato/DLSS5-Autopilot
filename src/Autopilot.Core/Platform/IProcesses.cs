namespace Autopilot.Core.Platform;

/// <summary>The processes running on the machine, so an install can refuse while the game runs.</summary>
public interface IProcesses
{
    /// <summary>Every running process this user can see, with its image path where it can be read.</summary>
    IReadOnlyList<RunningProcess> List();
}

/// <summary>A running process: its id, and the full path of its executable image, null when it cannot be read (another user's process, a protected one).</summary>
public sealed record RunningProcess(int Id, string? ImagePath);
