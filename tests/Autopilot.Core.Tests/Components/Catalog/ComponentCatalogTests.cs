using Autopilot.Core.Components;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.GitHub;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Components.Catalog;

/// <summary>The catalog as data: one row per source, its publisher, its checks, and the area's hosts.</summary>
[TestClass]
public sealed class ComponentCatalogTests(TestContext testContext)
{
    // The ids the install record stores (the staleness check compares these), then the rest.
    private static readonly string[] _recorded =
        ["reshade", "renodx", "renodx_sf", "dlssnr", "dlss", "dlssd", "dlssg", "bridge", "upstream", "standalone", "uplift", "feeder", "optiscaler", "remix_runtime"];

    private static readonly string[] _others = ["dxvk", "reframework", "mfg", "asi_loader", "lumenite", "vort", "shader_headers"];

    private static readonly GitHubSource _nvidia = GitHubSource.Parse("NVIDIA/DLSS");

    [TestMethod]
    public void Every_component_is_listed_and_every_source_of_it_once()
    {
        string[] pairs = [.. ComponentCatalog.All.Select(entry => $"{entry.Id} {entry.Source}")];

        CollectionAssert.AreEquivalent(_recorded.Concat(_others).ToArray(), ComponentCatalog.Ids.ToArray());
        CollectionAssert.AllItemsAreUnique(pairs);
    }

    [TestMethod]
    public void A_component_checked_by_nothing_says_why()
    {
        foreach (CatalogEntry entry in ComponentCatalog.All)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(entry.UncheckedReason), entry.Id);
            Assert.AreEqual(entry.Check is ComponentCheck.SumsRequired or ComponentCheck.SumsWhenPublished, entry.SumsPattern is not null, entry.Id);
        }

        Assert.AreEqual(ComponentCheck.SumsRequired, ComponentCatalog.Of("uplift").Check);
        Assert.AreEqual("umar-afzaal/LumeniteFX", ComponentCatalog.Of("lumenite").Source.ToString());
    }

    [TestMethod]
    public void The_area_has_one_host_list()
    {
        HostAllowlist hosts = ComponentHosts.Allowlist;

        Assert.IsTrue(hosts.Allows("codeload.github.com"));
        Assert.IsTrue(hosts.Allows("raw.githubusercontent.com"));
        Assert.IsFalse(hosts.Allows("example.com"));
    }

    [TestMethod]
    public void An_unknown_id_or_source_is_refused()
    {
        _ = Assert.ThrowsExactly<ArgumentException>(() => ComponentCatalog.Of("vulkan"));
        _ = Assert.ThrowsExactly<ArgumentException>(() => ComponentCatalog.Of("dlssnr", _nvidia));
    }

    [TestMethod]
    public void A_component_not_checked_by_its_release_data_has_no_release_check()
    {
        GitHubRelease release = new("v1", false, false, null, []);
        GitHubAsset asset = new("ReShade_Setup_6.7.3_Addon.exe", new("https://reshade.me/downloads/ReShade_Setup_6.7.3_Addon.exe"), null);

        _ = Assert.ThrowsExactly<ArgumentException>(() => ComponentCatalog.Of("reshade").VerificationOf(release, asset));
    }

    [TestMethod]
    public void Every_component_resolved_here_is_in_the_catalog()
    {
        using ComponentsBed bed = new(testContext);

        CollectionAssert.IsSubsetOf(bed.Sources.Ids.ToArray(), ComponentCatalog.All.Select(entry => entry.Id).ToArray());
        _ = Assert.ThrowsExactly<ArgumentException>(() => bed.Sources.ResolveAsync("reshade", bed.Cancellation));
    }
}
