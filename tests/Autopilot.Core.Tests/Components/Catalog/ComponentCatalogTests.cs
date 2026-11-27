using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.GitHub;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Components.Catalog;

/// <summary>The catalog as data: one entry per component, its publisher, its check, and the area's hosts.</summary>
[TestClass]
public sealed class ComponentCatalogTests(TestContext testContext)
{
    // The ids the install record stores (the staleness check compares these), then the rest.
    private static readonly string[] _recorded =
        ["reshade", "renodx", "renodx_sf", "dlssnr", "dlss", "dlssd", "dlssg", "bridge", "upstream", "standalone", "uplift", "feeder", "optiscaler", "remix_runtime"];

    private static readonly string[] _others = ["dxvk", "reframework", "mfg", "asi_loader", "lumenite", "vort", "shader_headers"];

    [TestMethod]
    public void Every_component_is_listed_once()
    {
        string[] ids = [.. ComponentCatalog.All.Select(entry => entry.Id)];

        CollectionAssert.AreEquivalent(_recorded.Concat(_others).ToArray(), ids);
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
        Assert.AreEqual(ComponentCheck.NvidiaSigner, ComponentCatalog.Of("dlssnr").Check);
        Assert.AreEqual("umar-afzaal/LumeniteFX", ComponentCatalog.Of("lumenite").Source);
    }

    [TestMethod]
    public void The_area_has_one_host_list()
    {
        HostAllowlist hosts = ComponentCatalog.Hosts;

        Assert.IsTrue(hosts.Allows("codeload.github.com"));
        Assert.IsTrue(hosts.Allows("raw.githubusercontent.com"));
        Assert.IsFalse(hosts.Allows("example.com"));
    }

    [TestMethod]
    public void An_unknown_id_is_refused() => _ = Assert.ThrowsExactly<ArgumentException>(() => ComponentCatalog.Of("vulkan"));

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
