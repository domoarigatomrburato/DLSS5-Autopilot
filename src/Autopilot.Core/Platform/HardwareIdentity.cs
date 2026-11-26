namespace Autopilot.Core.Platform;

/// <summary>The hardware values the EA app derives its install file's key from, in the order the key uses them.</summary>
/// <param name="BaseBoardManufacturer">The motherboard's maker.</param>
/// <param name="BaseBoardSerial">The motherboard's serial number.</param>
/// <param name="BiosManufacturer">The BIOS's maker.</param>
/// <param name="BiosSerial">The BIOS's serial number.</param>
/// <param name="SystemVolumeSerial">The serial number of drive C:'s volume, in uppercase hex without leading zeros.</param>
/// <param name="VideoControllerDeviceId">The graphics card's Plug and Play device id.</param>
/// <param name="ProcessorManufacturer">The processor's maker.</param>
/// <param name="ProcessorId">The processor's id.</param>
/// <param name="ProcessorName">The processor's name.</param>
public sealed record HardwareIdentity(
    string BaseBoardManufacturer,
    string BaseBoardSerial,
    string BiosManufacturer,
    string BiosSerial,
    string SystemVolumeSerial,
    string VideoControllerDeviceId,
    string ProcessorManufacturer,
    string ProcessorId,
    string ProcessorName);
