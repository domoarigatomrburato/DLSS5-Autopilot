namespace Autopilot.Core.Library.Emulators;

/// <summary>
/// A folder the emulator search looks in. A dedicated one is kept for emulators (an emulator's own
/// install folder, <c>D:\Emulators</c>), so a generic executable name there is the emulator; anywhere
/// else only a distinctive name is.
/// </summary>
internal sealed record EmulatorRoot(string Folder, bool Dedicated);
