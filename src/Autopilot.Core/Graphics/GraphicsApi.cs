namespace Autopilot.Core.Graphics;

/// <summary>The graphics API a game draws with, as the tool reads it from the game's files.</summary>
public enum GraphicsApi
{
    /// <summary>The files carried no evidence, or none the tool can act on.</summary>
    Unknown,
    DX8,
    DX9,
    DX10,
    DX11,
    DX12,

    /// <summary>Direct3D 11 or 12, not settled: the person settles it as one or the other.</summary>
    Dxgi,
    Vulkan,
    OpenGL,
}
