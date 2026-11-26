namespace Autopilot.Core.Graphics;

/// <summary>The kind of evidence that decided a game's graphics API.</summary>
public enum ApiReason
{
    /// <summary>No evidence: the API is <see cref="GraphicsApi.Unknown"/>.</summary>
    None,

    /// <summary>A graphics DLL in the executable's import table.</summary>
    StaticImport,

    /// <summary>A graphics DLL in the executable's delay-load import table.</summary>
    DelayLoad,

    /// <summary>A D3D12 Agility SDK, or the game's own DLSS Frame Generation or Ray Reconstruction, beside the executable.</summary>
    AgilitySdk,

    /// <summary>DLSS Frame Generation or Ray Reconstruction in NVIDIA's Unreal plugins.</summary>
    UnrealPlugin,

    /// <summary>The game's own DLSS runtime beside the executable.</summary>
    DlssFiles,

    /// <summary>A DLL beside the executable imports the renderer.</summary>
    SiblingImport,

    /// <summary>A Unity player beside the executable.</summary>
    Unity,

    /// <summary>An Unreal Engine 4 or 5 layout.</summary>
    Unreal,

    /// <summary>A 32-bit Unreal Engine 3 layout, and its config.</summary>
    Unreal3,

    /// <summary>An Unreal Engine 1 or 2 layout, its ini and its render device module.</summary>
    UnrealClassic,

    /// <summary>A renderer named in the executable's own bytes.</summary>
    RunTimeName,

    /// <summary>A renderer named in the bytes of a DLL beside the executable.</summary>
    SiblingString,
}
