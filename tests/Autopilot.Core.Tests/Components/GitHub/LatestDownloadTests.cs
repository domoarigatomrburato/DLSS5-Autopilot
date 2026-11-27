using Autopilot.Core.Components.GitHub;

namespace Autopilot.Core.Tests.Components.GitHub;

/// <summary>GitHub's <c>latest/download</c> redirect, the last resort of a component taken by asset name.</summary>
[TestClass]
public sealed class LatestDownloadTests
{
    [TestMethod]
    public void An_asset_name_is_escaped_into_the_address()
    {
        Uri url = GitHubRepository.Parse("owner/repo").LatestDownload("a b/../c.zip");

        Assert.AreEqual("/owner/repo/releases/latest/download/a%20b%2F..%2Fc.zip", url.AbsolutePath);
    }

    [TestMethod]
    public void A_repository_that_is_not_one_is_refused() =>
        _ = Assert.ThrowsExactly<ArgumentException>(() => GitHubRepository.Parse("../repo"));
}
