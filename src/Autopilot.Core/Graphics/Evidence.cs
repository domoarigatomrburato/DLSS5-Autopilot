namespace Autopilot.Core.Graphics;

/// <summary>A file beside the executable that says something about the renderer, with its kind and its name in words.</summary>
/// <param name="Reason">The kind of evidence.</param>
/// <param name="Words">The file named for a person, such as "nvngx_dlss.dll".</param>
internal sealed record Evidence(ApiReason Reason, string Words);
