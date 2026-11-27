using Autopilot.Core.Components;

namespace Autopilot.Core.Tests.Components;

[TestClass]
public sealed class LatestDownloadTests
{
    [TestMethod]
    public void The_last_resort_is_the_latest_download_redirect_with_a_version_that_is_not_comparable()
    {
        LatestDownload download = GitHubReleases.LatestDownloadOf("matiasLombo/neural-upstream", "nvngx.dll.addon64");

        Assert.AreEqual(new Uri("https://github.com/matiasLombo/neural-upstream/releases/latest/download/nvngx.dll.addon64"), download.Url);
        Assert.AreSame(ComponentVersion.Latest, download.Version);
        Assert.IsFalse(download.Version.IsComparable);
        Assert.AreEqual(VersionOrder.NotComparable, download.Version.CompareTo(new ComponentVersion("1.0.0")));
        Assert.Contains("the exact version is whatever that redirect points at", download.Notice);
    }

    [TestMethod]
    public void An_asset_name_is_escaped_into_the_address()
    {
        LatestDownload download = GitHubReleases.LatestDownloadOf("owner/repo", "a b/../c.zip");

        Assert.AreEqual("/owner/repo/releases/latest/download/a%20b%2F..%2Fc.zip", download.Url.AbsolutePath);
    }

    [TestMethod]
    public void A_repository_or_asset_that_is_not_one_is_refused()
    {
        _ = Assert.ThrowsExactly<ArgumentException>(() => GitHubReleases.LatestDownloadOf("../repo", "a.zip"));
        _ = Assert.ThrowsExactly<ArgumentException>(() => GitHubReleases.LatestDownloadOf("owner/repo", " "));
    }
}
