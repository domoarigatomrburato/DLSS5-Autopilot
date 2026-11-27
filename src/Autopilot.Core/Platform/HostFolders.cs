namespace Autopilot.Core.Platform;

/// <summary>
/// The machine's well-known folders (<c>%PROGRAMDATA%</c>, <c>%LOCALAPPDATA%</c>, <c>%APPDATA%</c>,
/// <c>%ProgramFiles%</c>, <c>%ProgramFiles(x86)%</c>, and the person's Desktop, Downloads and
/// Documents). A null folder is one the machine does not set, and the source that needs it is
/// skipped rather than read relative to the current folder.
/// </summary>
public sealed record HostFolders(
    string? ProgramData,
    string? LocalAppData,
    string? AppData,
    string? ProgramFiles,
    string? ProgramFilesX86,
    string? Desktop = null,
    string? Downloads = null,
    string? Documents = null);
