using Autopilot.Core.Platform;
using Autopilot.Core.Storage;

using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Storage;

/// <summary>The drives a full-drive failure names, read through a fake free-space answer per drive.</summary>
[TestClass]
public sealed class DiskFullPlacesTests
{
    [TestMethod]
    public void Each_drive_is_listed_once_with_what_it_holds_failed_drive_first()
    {
        FakeFreeSpace drives = new(("/cache", new("D:", 1L * 1024 * 1024 * 1024)), ("/games", new("C:", 5L * 1024 * 1024 * 1024)));
        DiskFullException refused = new("/cache/files/x.part", "/cache", new IOException("full"));

        IReadOnlyList<DrivePlace> places = refused.Places("/games/Arena", drives);

        CollectionAssert.AreEqual(
            new[]
            {
                new DrivePlace(new("D:", 1L * 1024 * 1024 * 1024), DriveRoles.FailedWrite | DriveRoles.Cache),
                new DrivePlace(new("C:", 5L * 1024 * 1024 * 1024), DriveRoles.Game),
            },
            places.ToArray());
        Assert.AreEqual(
            "Out of disk space on D: (1 GB free). The install needs room beside the game (C:, 5 GB free) and in the tool's download cache (D:, 1 GB free): free some up, then install again.",
            refused.Explain("/games/Arena", drives));
    }

    [TestMethod]
    public void A_drive_whose_free_space_is_unknown_says_so()
    {
        FakeFreeSpace drives = new(("/share", new(@"\\server\games", null)), ("/cache", new("C:", 512)));
        DiskFullException refused = new("/share/Arena/dxgi.dll", "/cache", new IOException("full"));

        IReadOnlyList<DrivePlace> places = refused.Places("/share/Arena", drives);

        CollectionAssert.AreEqual(
            new[] { DriveRoles.FailedWrite | DriveRoles.Game, DriveRoles.Cache },
            places.Select(place => place.Roles).ToArray());
        Assert.AreEqual(
            @"Out of disk space on \\server\games (free space unknown). The install needs room beside the game (\\server\games, free space unknown) and in the tool's download cache (C:, 512 bytes free): free some up, then install again.",
            refused.Explain("/share/Arena", drives));
    }

    [TestMethod]
    public void A_write_outside_the_game_and_the_cache_names_only_its_drive()
    {
        FakeFreeSpace drives = new(("/elsewhere", new("E:", 2048)));
        DiskFullException refused = new("/elsewhere/file", cacheFolder: null, new IOException("full"));

        Assert.AreEqual(new DrivePlace(new("E:", 2048), DriveRoles.FailedWrite), refused.Places(gameFolder: null, drives).Single());
        Assert.AreEqual("Out of disk space on E: (2 KB free). Free some up, then install again.", refused.Explain(gameFolder: null, drives));
    }

    [TestMethod]
    public void The_portable_drives_name_the_drive_holding_a_path_with_its_free_space()
    {
        DriveSpace drive = new DriveFreeSpace().DriveOf(Path.GetTempPath());

        Assert.IsFalse(string.IsNullOrEmpty(drive.Name));
        Assert.IsNotNull(drive.FreeBytes);
        Assert.IsGreaterThanOrEqualTo(0L, drive.FreeBytes.Value);
    }

    [TestMethod]
    [DataRow("/Volumes/Games/Arena", "/Volumes/Games", true)]
    [DataRow("/Volumes/Games", "/Volumes/Games/", true)]
    [DataRow("/Volumes/Games/", "/Volumes/Games", true)]
    [DataRow("/Volumes/GamesSSD/Arena", "/Volumes/Games", false)]
    [DataRow("/Volumes/GamesSSD", "/Volumes/Games/", false)]
    [DataRow("/Volumes/Games", "/", true)]
    [DataRow(@"D:\Games\Arena", @"D:\", true)]
    [DataRow(@"D:\GamesSSD\Arena", @"D:\Games", false)]
    public void A_path_is_on_a_drive_by_whole_parts_not_by_prefix(string path, string root, bool expected) =>
        Assert.AreEqual(expected, PathParts.IsAtOrBelow(root, path), "the drive choice's own comparison");
}
