using Autopilot.Core.Archives;

namespace Autopilot.Core.Tests.Archives;

[TestClass]
public sealed class ArchiveLimitTests(TestContext testContext) : ArchiveTestBase(testContext)
{
    [TestMethod]
    [DynamicData(nameof(Formats))]
    public async Task An_archive_with_more_entries_than_the_limit_writes_nothing(ArchiveFormat format)
    {
        string archive = Build(format, TestArchives.ManyFiles(6));
        string destination = In("out");

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ExtractTreeAsync(archive, destination, limits: new ArchiveLimits { MaxEntries = 5 }, ct: Ct));

        Assert.AreEqual(ArchiveFailure.TooManyEntries, failure.Reason);
        AssertNothingWritten(destination);
    }

    [TestMethod]
    [DynamicData(nameof(Formats))]
    public async Task An_archive_that_expands_past_the_size_limit_writes_nothing(ArchiveFormat format)
    {
        string archive = Build(format, new TestMember("a.bin", new byte[600]), new TestMember("b.bin", new byte[600]));
        string destination = In("b.bin");

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ExtractMemberAsync(archive, "b.bin", destination, new ArchiveLimits { MaxTotalBytes = 1000 }, Ct));

        Assert.AreEqual(ArchiveFailure.TooLarge, failure.Reason);
        AssertNothingWritten(destination);
    }

    [TestMethod]
    [DynamicData(nameof(Formats))]
    public async Task A_highly_compressible_archive_past_the_ratio_writes_nothing(ArchiveFormat format)
    {
        string archive = Build(format, new TestMember("zeros.bin", TestArchives.Zeros(2 << 20)));
        string destination = In("out");

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ExtractTreeAsync(archive, destination, limits: new ArchiveLimits { MaxRatio = 10 }, ct: Ct));

        Assert.AreEqual(ArchiveFailure.RatioExceeded, failure.Reason);
        AssertNothingWritten(destination);
    }

    [TestMethod]
    [DynamicData(nameof(Formats))]
    public async Task A_compressible_archive_under_the_default_limits_unpacks(ArchiveFormat format)
    {
        string archive = Build(format, new TestMember("small.bin", TestArchives.Zeros(64 << 10)));
        string destination = In("small.bin");

        string written = await ArchiveExtractor.ExtractMemberAsync(archive, "small.bin", destination, ct: Ct);

        Assert.AreEqual(destination, written);
        Assert.AreEqual(64 << 10, new FileInfo(destination).Length);
    }

    [TestMethod]
    public void A_limit_raised_above_its_default_is_rejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ArchiveLimits { MaxEntries = 50_001 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ArchiveLimits { MaxTotalBytes = (16L << 30) + 1 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ArchiveLimits { MaxRatio = 201 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ArchiveLimits { RatioCheckedAbove = (1 << 20) + 1 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ArchiveLimits { MaxEntries = 0 });
    }

    [TestMethod]
    public void The_default_limits_are_the_documented_ones()
    {
        ArchiveLimits limits = ArchiveLimits.Default;

        Assert.AreEqual(50_000, limits.MaxEntries);
        Assert.AreEqual(16L << 30, limits.MaxTotalBytes);
        Assert.AreEqual(200, limits.MaxRatio);
        Assert.AreEqual(1L << 20, limits.RatioCheckedAbove);
    }
}
