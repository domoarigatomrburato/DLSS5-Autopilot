using Autopilot.Core.Resources;

namespace Autopilot.Core.Storage;

/// <summary>
/// A write that found its drive full. <see cref="Exception.Message"/> is a short English line for
/// the log; <see cref="Explain"/> gives the words a person reads, and <see cref="Places"/> the drives behind them.
/// </summary>
public sealed class DiskFullException(string failedPath, string? cacheFolder, Exception innerException)
    : IOException($"{failedPath}: the drive is full", innerException)
{
    /// <summary>The file or folder whose write failed.</summary>
    public string FailedPath { get; } = failedPath;

    /// <summary>The download cache's folder, whose drive the explanation names, when the write was the cache's.</summary>
    public string? CacheFolder { get; } = cacheFolder;

    /// <summary>
    /// One entry per distinct drive, in the order: the drive the failed write was on, then those of
    /// <paramref name="gameFolder"/> (when given) and of the cache (when known), each with what it holds.
    /// </summary>
    public IReadOnlyList<DrivePlace> Places(string? gameFolder, IFreeSpace space)
    {
        ArgumentNullException.ThrowIfNull(space);
        List<DrivePlace> places = [];
        Add(places, space.DriveOf(FailedPath), DriveRoles.FailedWrite);
        if (gameFolder is not null)
        {
            Add(places, space.DriveOf(gameFolder), DriveRoles.Game);
        }
        if (CacheFolder is not null)
        {
            Add(places, space.DriveOf(CacheFolder), DriveRoles.Cache);
        }
        return places;
    }

    /// <summary>
    /// Names the drive the failed write was on, then the drives of <paramref name="gameFolder"/>
    /// (when given) and of the cache with their free space, and ends with what to do.
    /// </summary>
    public string Explain(string? gameFolder, IFreeSpace space)
    {
        IReadOnlyList<DrivePlace> places = Places(gameFolder, space);
        string head = Texts.Format("DiskFullOn", places[0].Drive.Name, Free(places[0].Drive));
        // The game's drive is named before the cache's, whichever the failed write was on.
        string[] drives =
        [
            .. places
                .Where(place => (place.Roles & (DriveRoles.Game | DriveRoles.Cache)) != 0)
                .OrderBy(place => place.Roles.HasFlag(DriveRoles.Game) ? 0 : 1)
                .Select(place => Texts.Format("DiskFullPlace", Labels(place.Roles), place.Drive.Name, Free(place.Drive))),
        ];
        return drives.Length == 0
            ? head + " " + Texts.Text("DiskFullFreeSome")
            : head + " " + Texts.Format("DiskFullNeedsRoom", string.Join(Texts.Text("DiskFullAnd"), drives));
    }

    /// <summary>A size as people read it: "1.5 GB", "120 MB", "512 bytes".</summary>
    internal static string HumanSize(long bytes)
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

    // A drive already listed gains the role, so a game and a cache on one drive show its free space once.
    private static void Add(List<DrivePlace> places, DriveSpace drive, DriveRoles role)
    {
        int at = places.FindIndex(place => string.Equals(place.Drive.Name, drive.Name, StringComparison.OrdinalIgnoreCase));
        if (at < 0)
        {
            places.Add(new(drive, role));
        }
        else
        {
            places[at] = places[at] with { Roles = places[at].Roles | role };
        }
    }

    private static string Labels(DriveRoles roles)
    {
        List<string> labels = [];
        if (roles.HasFlag(DriveRoles.Game))
        {
            labels.Add(Texts.Text("DiskFullGamePlace"));
        }
        if (roles.HasFlag(DriveRoles.Cache))
        {
            labels.Add(Texts.Text("DiskFullCachePlace"));
        }
        return string.Join(Texts.Text("DiskFullAnd"), labels);
    }

    private static string Free(DriveSpace drive) =>
        drive.FreeBytes is { } free ? Texts.Format("DiskFree", HumanSize(free)) : Texts.Text("DiskFreeUnknown");
}
