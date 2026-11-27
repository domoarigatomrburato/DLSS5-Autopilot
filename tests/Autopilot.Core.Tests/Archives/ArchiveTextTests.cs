using Autopilot.Core.Archives;

namespace Autopilot.Core.Tests.Archives;

/// <summary>What a person reads when an archive is refused: from the resources, naming the archive and the member.</summary>
[TestClass]
public sealed class ArchiveTextTests(TestContext testContext) : ArchiveTestBase(testContext)
{
    private static readonly ArchiveFailure[] _namingAMember =
    [
        ArchiveFailure.UnsafeName,
        ArchiveFailure.DuplicateName,
        ArchiveFailure.Link,
        ArchiveFailure.UnsupportedEntry,
        ArchiveFailure.TooLarge,
        ArchiveFailure.RatioExceeded,
        ArchiveFailure.MemberNotFound,
        ArchiveFailure.AmbiguousMember,
    ];

    [TestMethod]
    public void Every_refusal_has_a_text_naming_the_archive_and_the_member_where_there_is_one()
    {
        foreach (ArchiveFailure reason in Enum.GetValues<ArchiveFailure>())
        {
            string whole = new ArchiveException(reason, "tool.zip", member: null, "log line").Explain();
            string member = new ArchiveException(reason, "tool.zip", "bin/x64/tool.dll", "log line").Explain();

            StringAssert.StartsWith(whole, "tool.zip ", StringComparison.Ordinal, $"{reason}");
            Assert.DoesNotContain("{", whole, $"{reason}");
            StringAssert.StartsWith(member, "tool.zip ", StringComparison.Ordinal, $"{reason}");
            Assert.AreEqual(_namingAMember.Contains(reason), member.Contains("'bin/x64/tool.dll'", StringComparison.Ordinal), $"{reason}");
        }
    }

    [TestMethod]
    public void A_limit_passed_by_the_whole_archive_does_not_name_a_member()
    {
        string text = new ArchiveException(ArchiveFailure.RatioExceeded, "pack.7z", member: null, "log line").Explain();

        Assert.AreEqual("pack.7z unpacks to far more than its own size, as no ordinary download does, so nothing was unpacked. Report it if it keeps happening.", text);
    }

    [TestMethod]
    public async Task A_refused_member_is_named_in_the_text_and_the_log_line_stays_short()
    {
        string archive = Build(ArchiveFormat.Zip, TestMember.Text("dxgi.dll", "a"), TestMember.Text("plugins/DXGI.dll", "b"));
        string destination = In("dxgi.dll");

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ExtractMemberAsync(archive, "dxgi.dll", destination, limits: null, Ct));

        Assert.AreEqual(Path.GetFileName(archive), failure.ArchiveName);
        Assert.AreEqual("plugins/DXGI.dll", failure.Member);
        Assert.AreEqual(
            $"{failure.ArchiveName} holds more than one file that could be the one needed, the second being 'plugins/DXGI.dll', so nothing was unpacked. The publisher may have changed the package: report it.",
            failure.Explain());
        StringAssert.StartsWith(failure.Message, failure.ArchiveName + ": more than one member ends with 'dxgi.dll'", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task A_member_past_the_ratio_is_named()
    {
        string archive = Build(ArchiveFormat.Zip, new TestMember("zeros.bin", TestArchives.Zeros(2 << 20)));

        ArchiveException failure = await Assert.ThrowsExactlyAsync<ArchiveException>(
            () => ArchiveExtractor.ListAsync(archive, new ArchiveLimits { MaxRatio = 10 }, Ct));

        Assert.AreEqual("zeros.bin", failure.Member);
        StringAssert.Contains(failure.Explain(), "holds 'zeros.bin', which unpacks to far more than its packed size", StringComparison.Ordinal);
    }
}
