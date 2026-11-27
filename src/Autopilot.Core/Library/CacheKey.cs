namespace Autopilot.Core.Library;

/// <summary>What a saved library must have been saved under to be used: the tool's version and the GPU.</summary>
/// <param name="ToolVersion">The version of this tool; what it believes about games changes between releases.</param>
/// <param name="GpuSm">The GPU's compute capability (<c>sm</c>), or null when no GPU was read.</param>
public sealed record CacheKey(string ToolVersion, int? GpuSm);
