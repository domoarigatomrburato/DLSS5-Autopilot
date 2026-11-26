using Autopilot.Core.Resources;

namespace Autopilot.Core.Storage;

/// <summary>
/// A write that found its drive full. <see cref="Exception.Message"/> is a short English line for
/// the log; <see cref="Explain"/> gives the words a person reads.
/// </summary>
public sealed class DiskFullException(string failedPath, string? cacheFolder, Exception innerException)
    : IOException($"{failedPath}: the drive is full", innerException)
{
    /// <summary>The file or folder whose write failed.</summary>
    public string FailedPath { get; } = failedPath;

    /// <summary>The download cache's folder, whose drive the explanation names, when the write was the cache's.</summary>
    public string? CacheFolder { get; } = cacheFolder;

    /// <summary>
    /// Names the drive the failed write was on, then the drives of <paramref name="gameFolder"/>
    /// (when given) and of the cache with their free space, and ends with what to do.
    /// </summary>
    public string Explain(string? gameFolder)
    {
        DriveInfo[] mounted = DriveInfo.GetDrives();
        Place failed = PlaceOf(FailedPath, mounted);
        string head = Texts.Format("DiskFullOn", failed.Name, failed.Free());
        List<(string Label, Place Place)> places = [];
        if (gameFolder is not null)
        {
            places.Add((Texts.Text("DiskFullGamePlace"), PlaceOf(gameFolder, mounted)));
        }
        if (CacheFolder is not null)
        {
            places.Add((Texts.Text("DiskFullCachePlace"), PlaceOf(CacheFolder, mounted)));
        }
        if (places.Count == 0)
        {
            return head + " " + Texts.Text("DiskFullFreeSome");
        }
        // One entry per drive, so a game and a cache on the same drive show its free space once.
        IEnumerable<string> drives = places
            .GroupBy(place => place.Place.Name, StringComparer.OrdinalIgnoreCase)
            .Select(drive => Texts.Format(
                "DiskFullPlace",
                string.Join(Texts.Text("DiskFullAnd"), drive.Select(place => place.Label)),
                drive.Key,
                drive.First().Place.Free()));
        return head + " " + Texts.Format("DiskFullNeedsRoom", string.Join(Texts.Text("DiskFullAnd"), drives));
    }

    /// <summary>A size as people read it: "1.5 GB", "120 MB", "512 bytes".</summary>
    private static string HumanSize(long bytes)
    {
        string[] units = ["SizeKilobytes", "SizeMegabytes", "SizeGigabytes", "SizeTerabytes"];
        double size = bytes;
        int unit = -1;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return unit < 0 ? Texts.Format("SizeBytes", bytes) : Texts.Format(units[unit], size);
    }

    // The mounted drive holding the path: the longest root it starts with. A path on no mounted
    // drive, such as a network share, is named by its root, with its free space unknown.
    private static Place PlaceOf(string path, DriveInfo[] mounted)
    {
        string full = Path.GetFullPath(path);
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        DriveInfo? drive = mounted
            .Where(drive => full.StartsWith(drive.RootDirectory.FullName, comparison))
            .MaxBy(drive => drive.RootDirectory.FullName.Length);
        return drive is null ? new(Path.GetPathRoot(full) ?? full, null) : new(DisplayName(drive), drive);
    }

    // "C:" rather than "C:\", as Windows names a drive in prose; "/" stays as it is.
    private static string DisplayName(DriveInfo drive) =>
        drive.Name.Length > 1 ? drive.Name.TrimEnd('\\') : drive.Name;

    private static string FreeOn(DriveInfo? drive)
    {
        if (drive is null)
        {
            return Texts.Text("DiskFreeUnknown");
        }

        try
        {
            return Texts.Format("DiskFree", HumanSize(drive.AvailableFreeSpace));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Texts.Text("DiskFreeUnknown");
        }
    }

    private sealed record Place(string Name, DriveInfo? Drive)
    {
        public string Free() => FreeOn(Drive);
    }
}
