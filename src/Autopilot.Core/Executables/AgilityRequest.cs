namespace Autopilot.Core.Executables;

/// <summary>
/// The Direct3D 12 Agility SDK an executable asks Windows for, through its <c>D3D12SDKVersion</c>
/// and <c>D3D12SDKPath</c> exports. <paramref name="Path"/> is as exported, relative to the
/// executable's folder; it is empty when the path export is missing or unreadable, which the
/// D3D12 loader treats as the executable's own folder.
/// </summary>
public sealed record AgilityRequest(uint Version, string Path);
