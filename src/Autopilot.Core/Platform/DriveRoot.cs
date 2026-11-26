namespace Autopilot.Core.Platform;

/// <summary>A drive of the machine, by the root folder its paths start from (<c>D:\</c>).</summary>
public sealed record DriveRoot(string Root, DriveKind Kind);
