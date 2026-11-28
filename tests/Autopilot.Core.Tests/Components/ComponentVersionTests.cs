using Autopilot.Core.Components;

namespace Autopilot.Core.Tests.Components;

[TestClass]
public sealed class ComponentVersionTests
{
    [TestMethod]
    [DataRow("310.8.0", "310.7.9", VersionOrder.Newer, DisplayName = "by integers, not text")]
    [DataRow("310.7.9", "310.8.0", VersionOrder.Older)]
    [DataRow("v3.10", "v3.9", VersionOrder.Newer, DisplayName = "10 above 9")]
    [DataRow("v0.9.0-beta.1", "v0.9.0", VersionOrder.Older, DisplayName = "a beta below its release")]
    [DataRow("v0.9.0", "v0.9.0-beta.1", VersionOrder.Newer)]
    [DataRow("v0.9.0-beta.2", "v0.9.0-beta.1", VersionOrder.Newer, DisplayName = "betas by their own number")]
    [DataRow("v1.4.13-pre8", "v1.4.12", VersionOrder.Newer, DisplayName = "a test build of a higher number")]
    [DataRow("1.16.0-beta.5", "v0.15.1", VersionOrder.Newer)]
    [DataRow("v4.55", "4.55", VersionOrder.Same, DisplayName = "a leading v")]
    [DataRow("310.8", "310.8.0", VersionOrder.Same, DisplayName = "a missing integer is zero")]
    [DataRow("1.2.3.4.9", "1.2.3.4.1", VersionOrder.Same, DisplayName = "the first four integers only")]
    [DataRow("1.2.3.5", "1.2.3.4", VersionOrder.Newer, DisplayName = "the fourth integer counts")]
    [DataRow("10.0.17763.1", "10.0.26100.1", VersionOrder.Older, DisplayName = "a file version of four parts")]
    [DataRow("99999999999999999999999.0", "1.0", VersionOrder.Newer, DisplayName = "a number past any integer type")]
    [DataRow("007", "7", VersionOrder.Same, DisplayName = "leading zeros")]
    public void Versions_compare_by_their_integers(string version, string other, VersionOrder expected) =>
        Assert.AreEqual(expected, new ComponentVersion(version).CompareTo(new(other)));

    [TestMethod]
    [DataRow("SF", "1.0", DisplayName = "a label without numbers")]
    [DataRow("stable", "edge", DisplayName = "two labels without numbers")]
    public void Labels_that_are_not_numbers_are_different_never_older(string version, string other)
    {
        Assert.AreEqual(VersionOrder.Different, new ComponentVersion(version).CompareTo(new(other)));
        Assert.AreEqual(VersionOrder.Different, new ComponentVersion(other).CompareTo(new(version)));
    }

    [TestMethod]
    public void The_same_label_without_numbers_is_the_same() =>
        Assert.AreEqual(VersionOrder.Same, new ComponentVersion("SF").CompareTo(new("sf")));

    [TestMethod]
    [DataRow("latest")]
    [DataRow("nightly")]
    [DataRow("Latest")]
    public void Latest_and_nightly_compare_with_nothing(string label)
    {
        ComponentVersion version = new(label);

        Assert.IsFalse(version.IsComparable);
        Assert.AreEqual(VersionOrder.NotComparable, version.CompareTo(new("1.0")));
        Assert.AreEqual(VersionOrder.NotComparable, new ComponentVersion("1.0").CompareTo(version));
        Assert.AreEqual(VersionOrder.NotComparable, version.CompareTo(version));
    }

    [TestMethod]
    [DataRow("v0.9.0-beta.1", true)]
    [DataRow("1.4.13-pre8", true)]
    [DataRow("v2.0-RC1", true)]
    [DataRow("v1.0.0-alpha", true)]
    [DataRow("6.0-preview", true)]
    [DataRow("v3.2", false)]
    [DataRow("310.8.0-RTX40", false)]
    public void A_test_build_is_named_in_its_label(string label, bool testBuild) =>
        Assert.AreEqual(testBuild, new ComponentVersion(label).IsTestBuild);

    [TestMethod]
    public void The_label_is_kept_as_written() =>
        Assert.AreEqual("v0.9.0-beta.1", new ComponentVersion("v0.9.0-beta.1").ToString());

    [TestMethod]
    public void A_commit_is_the_same_or_different_never_older()
    {
        ComponentVersion one = new("a6d42fc9851b55fcb134ab8bb1199122fafbda13");

        Assert.AreEqual(VersionOrder.Different, one.CompareTo(new("ff42fc9851b55fcb134ab8bb1199122fafbda131")));
        Assert.AreEqual(VersionOrder.Same, one.CompareTo(new("A6D42FC9851B55FCB134AB8BB1199122FAFBDA13")));
        Assert.AreEqual(VersionOrder.Different, new ComponentVersion("1.0.0").CompareTo(one));
    }
}
