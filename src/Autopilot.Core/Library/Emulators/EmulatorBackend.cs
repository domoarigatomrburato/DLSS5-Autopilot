namespace Autopilot.Core.Library.Emulators;

/// <summary>The render backend an emulator's config file names, for the game's status.</summary>
/// <param name="Name">The backend in the status's words ("D3D12", "Vulkan"), or "unknown" when no config file or no value was found.</param>
/// <param name="AlreadyDxgi">Whether it already presents through Direct3D 11 or 12, so the Direct3D routes apply as they are.</param>
/// <param name="ConfigFile">The config file it was read from; null when none was found.</param>
public sealed record EmulatorBackend(string Name, bool AlreadyDxgi, string? ConfigFile);
