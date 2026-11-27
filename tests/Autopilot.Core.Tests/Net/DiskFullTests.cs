using Autopilot.Core.Storage;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Net;

[TestClass]
public sealed class DiskFullTests(TestContext testContext)
{
    private static readonly int _windowsDiskFull = DiskFullPoints.Code(DiskFullError.Windows112);
    private static readonly int _windowsHandleDiskFull = DiskFullPoints.Code(DiskFullError.Windows39);
    private static readonly int _unixNoSpace = DiskFullPoints.Code(DiskFullError.Errno28);
    private const string Instruction = "free some up, then install again.";

    public static IEnumerable<object[]> EveryFullDrive()
    {
        foreach (int code in new[] { _windowsDiskFull, _windowsHandleDiskFull, _unixNoSpace })
        {
            yield return [code, new IOException("full", code)];
            yield return [code, new IOException("could not write the file", new IOException("full", code))];
            yield return [code, new AggregateException(new InvalidOperationException("other"), new IOException("full", code))];
            yield return [code, new InvalidDataException("zip", new AggregateException(new IOException("full", code)))];
        }
    }

    [TestMethod]
    [DynamicData(nameof(EveryFullDrive))]
    public void A_full_drive_is_recognised_however_it_was_wrapped(int code, Exception failure) =>
        Assert.AreEqual(code != _unixNoSpace || !OperatingSystem.IsWindows(), IoErrors.IsDiskFull(failure));

    [TestMethod]
    public void Other_write_failures_are_not_a_full_drive()
    {
        Assert.IsFalse(IoErrors.IsDiskFull(new IOException("denied", unchecked((int)0x80070005))));
        Assert.IsFalse(IoErrors.IsDiskFull(new InvalidOperationException("no", new UnauthorizedAccessException())));
    }

    [TestMethod]
    [DynamicData(nameof(EveryFullDrive))]
    public async Task A_download_that_fills_the_drive_is_removed_and_said_as_such(int code, Exception failure)
    {
        if (code == _unixNoSpace && OperatingSystem.IsWindows())
        {
            return;
        }
        using CacheBed bed = new CacheBed(testContext).Serve(CacheFixtures.Zip);

        DiskFullException refused = await bed.DownloadFillsTheDrive(new FullDrive(failure));

        Assert.AreEqual(bed.Files.ZipEntry + ".part", refused.FailedPath);
        Assert.AreEqual(bed.Bed.CacheRoot, refused.CacheFolder);
        Assert.IsEmpty(bed.Files.Cached, "the partial file is removed");
        Assert.AreEqual(1, bed.Requests, "a full drive is never retried");
    }

    [TestMethod]
    public async Task A_full_drive_names_the_drives_and_what_to_do()
    {
        using CacheBed bed = new CacheBed(testContext).Serve(CacheFixtures.Zip);
        DiskFullException refused = await bed.DownloadFillsTheDrive(new FullDrive(new IOException("full", _windowsDiskFull)));

        string explanation = refused.Explain(gameFolder: Path.GetTempPath(), new DriveFreeSpace());

        StringAssert.StartsWith(explanation, "Out of disk space on ", StringComparison.Ordinal);
        StringAssert.Contains(explanation, "beside the game and in the tool's download cache (", StringComparison.Ordinal);
        StringAssert.EndsWith(explanation, Instruction, StringComparison.Ordinal);
    }

    [TestMethod]
    public void A_full_drive_lists_each_drive_with_its_free_space_once()
    {
        string folder = Path.GetTempPath();

        string explanation = new DiskFullException(folder, folder, new IOException("full")).Explain(folder, new DriveFreeSpace());

        Assert.AreEqual(2, explanation.Split(" free)").Length - 1, "the full drive, then one entry for the game and the cache together");
        StringAssert.Contains(explanation, "The install needs room beside the game and in the tool's download cache (", StringComparison.Ordinal);
    }

    [TestMethod]
    public void Without_a_game_only_the_cache_is_named()
    {
        string folder = Path.GetTempPath();

        string explanation = new DiskFullException(folder, folder, new IOException("full")).Explain(gameFolder: null, new DriveFreeSpace());

        Assert.DoesNotContain("beside the game", explanation);
        StringAssert.Contains(explanation, "in the tool's download cache (", StringComparison.Ordinal);
        StringAssert.EndsWith(explanation, Instruction, StringComparison.Ordinal);
    }

    [TestMethod]
    public void A_full_drive_outside_the_cache_and_the_game_still_says_what_to_do()
    {
        string folder = Path.GetTempPath();

        string explanation = new DiskFullException(folder, cacheFolder: null, new IOException("full")).Explain(gameFolder: null, new DriveFreeSpace());

        StringAssert.StartsWith(explanation, "Out of disk space on ", StringComparison.Ordinal);
        Assert.DoesNotContain("download cache", explanation, "no cache folder is guessed");
        StringAssert.EndsWith(explanation, "Free some up, then install again.", StringComparison.Ordinal);
    }
}
