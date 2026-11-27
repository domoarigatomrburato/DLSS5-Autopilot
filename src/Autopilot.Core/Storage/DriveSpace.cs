using Autopilot.Core.Platform;

namespace Autopilot.Core.Storage;

/// <summary>The drive holding a path, named as people read it (<c>C:</c>, <c>/</c>), with its free bytes when known.</summary>
/// <param name="Name">The drive's name; a path on no mounted drive, such as a network share, is named by its root.</param>
/// <param name="FreeBytes">The bytes free for the current user; null when unknown (no mounted drive matches, or the query failed).</param>
public sealed record DriveSpace(string Name, long? FreeBytes);

/// <summary>Answers which drive holds a path and how much room it has. Tests pass a fake.</summary>
public interface IFreeSpace
{
    /// <summary>The drive holding <paramref name="path"/>, which need not exist yet.</summary>
    DriveSpace DriveOf(string path);
}

/// <summary>The drives as <see cref="DriveInfo"/> reports them, on any host.</summary>
public sealed class DriveFreeSpace : IFreeSpace
{
    /// <summary>The mounted drive holding the path: the longest root it starts with.</summary>
    /// <remarks>Roots are matched by Windows path rules (case-insensitive, either slash), which only approximate case-sensitive Unix mounts: those are development hosts only.</remarks>
    public DriveSpace DriveOf(string path)
    {
        string full = Path.GetFullPath(path);
        DriveInfo? drive = DriveInfo.GetDrives()
            .Where(drive => PathParts.IsAtOrBelow(drive.RootDirectory.FullName, full))
            .MaxBy(drive => drive.RootDirectory.FullName.Length);
        return drive is null ? new(Path.GetPathRoot(full) ?? full, null) : new(DisplayName(drive), FreeOn(drive));
    }

    // "C:" rather than "C:\", as Windows names a drive in prose; "/" stays as it is.
    private static string DisplayName(DriveInfo drive) =>
        drive.Name.Length > 1 ? drive.Name.TrimEnd('\\') : drive.Name;

    private static long? FreeOn(DriveInfo drive)
    {
        try
        {
            return drive.AvailableFreeSpace;
        }
        catch (Exception e) when (DrivePaths.IsNotThere(e))
        {
            return null;
        }
    }
}
