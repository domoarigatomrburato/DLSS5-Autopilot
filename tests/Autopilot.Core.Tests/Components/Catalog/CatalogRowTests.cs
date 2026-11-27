using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.Mirror;
using Autopilot.Core.Components.Sources;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Components.Catalog;

/// <summary>The catalog's rows: one per source, typed, each with the checks of its download and of the file inside it.</summary>
[TestClass]
public sealed class CatalogRowTests
{
    private static readonly GitHubSource _mirror = GitHubSource.Parse("RankFTW/rhi-repo");
    private static readonly GitHubSource _nvidia = GitHubSource.Parse("NVIDIA/DLSS");

    [TestMethod]
    public void No_rows_source_is_reached_through_a_member_that_throws()
    {
        foreach (CatalogEntry entry in ComponentCatalog.All)
        {
            Assert.IsTrue(entry.Source is GitHubSource || (entry.Source is WebSource && entry.Id == "reshade"), entry.Id);
            StringAssert.StartsWith(entry.Unchecked().Reason, entry.Source + ": ", StringComparison.Ordinal);
            StringAssert.Contains(entry.LayoutChanged("v1", "a.dll").Message, entry.Source.ToString(), StringComparison.Ordinal);
            StringAssert.Contains(entry.NoneCarries("a.zip", null).Message, entry.Source.ToString(), StringComparison.Ordinal);
        }

        Assert.AreEqual("reshade.me", ComponentCatalog.Of("reshade").Source.ToString());
        _ = Assert.ThrowsExactly<InvalidOperationException>(() => ComponentCatalog.Of("reshade").Repository, "a web site has no repository");
        Assert.AreEqual("crosire/reshade-shaders", ComponentCatalog.Of("shader_headers").Repository.ToString());
    }

    [TestMethod]
    [DataRow("dlss")]
    [DataRow("dlssd")]
    [DataRow("dlssg")]
    public void Nvidias_runtimes_have_nvidias_row_and_the_mirrors_both_checked_by_nvidias_signer(string id)
    {
        CatalogEntry nvidia = ComponentCatalog.Of(id, _nvidia);
        CatalogEntry mirror = ComponentCatalog.Of(id, _mirror);

        Assert.AreEqual((ComponentCheck.NvidiaSigner, ContentsCheck.None, "NVIDIA"), (nvidia.Check, nvidia.Contents, nvidia.Publisher));
        Assert.AreEqual((ComponentCheck.ReleaseDigest, ContentsCheck.NvidiaSigner, "NVIDIA, through the mirror"), (mirror.Check, mirror.Contents, mirror.Publisher));
        Assert.AreEqual(new SignerVerification("NVIDIA Corporation"), nvidia.Signer);
        _ = Assert.ThrowsExactly<ArgumentException>(() => ComponentCatalog.Of(id), "a component with several sources needs its source named");
    }

    [TestMethod]
    public void The_mirrors_zips_are_checked_by_their_digest_and_the_dll_inside_by_its_own_check()
    {
        Assert.AreEqual((ComponentCheck.ReleaseDigest, ContentsCheck.PinnedDigestOrNvidiaSigner), (ComponentCatalog.Of("dlssnr").Check, ComponentCatalog.Of("dlssnr").Contents));
        Assert.AreEqual((ComponentCheck.ReleaseDigest, ContentsCheck.None), (ComponentCatalog.Of("renodx").Check, ComponentCatalog.Of("renodx").Contents));
        Assert.AreEqual((ComponentCheck.ReleaseDigest, ContentsCheck.None), (ComponentCatalog.Of("renodx_sf").Check, ComponentCatalog.Of("renodx_sf").Contents));
        Assert.IsTrue(ComponentCatalog.All.Where(entry => entry.Source != _mirror).All(entry => entry.Contents == ContentsCheck.None), "only the mirror's rows check a file inside the download");
    }

    [TestMethod]
    public void Every_mirror_family_has_a_mirror_row()
    {
        foreach (BuildFamily family in Enum.GetValues<BuildFamily>())
        {
            CatalogEntry row = MirrorFamilies.EntryOf(family);

            Assert.AreEqual(_mirror, row.Source, family.ToString());
            Assert.AreEqual(ComponentCheck.ReleaseDigest, row.Check, family.ToString());
        }
    }

    [TestMethod]
    [DataRow("", "Dagherbou/OptiScaler_DLSSNR", ComponentCheck.SumsWhenPublished)]
    [DataRow("y4my4my4m", "y4my4my4m/OptiScaler_DLSSNR_Multipass_MFG", ComponentCheck.SumsWhenPublished)]
    [DataRow("wilsjo2", "wilsjo2/OptiScaler-DLSSNR-PreSR-Multipass", ComponentCheck.SumsWhenPublished)]
    [DataRow("wilsjo2-mfg", "wilsjo2/OptiScaler-DLSSNR-PreSR-Multipass", ComponentCheck.SumsWhenPublished)]
    [DataRow("janblade", "Janblade/OptiScaler-F5-DLSSNR-Multipass", ComponentCheck.SumsWhenPublished)]
    [DataRow("jlrouzies", "jlrouzies-fr/OptiScaler-DLSSNR-PreSR-Multipass", ComponentCheck.SumsRequired)]
    public void Every_optiscaler_build_has_its_pages_row(string key, string repository, ComponentCheck check)
    {
        OptiScalerBuild build = OptiScalerBuild.ByKey(key)!;

        Assert.AreSame(ComponentCatalog.Of("optiscaler", GitHubSource.Parse(repository)), build.Row);
        Assert.AreEqual((check, "*SHA256SUMS*"), (build.Row.Check, build.Row.SumsPattern));
        Assert.AreEqual(check == ComponentCheck.SumsRequired, build.PublishesSums);
    }

    [TestMethod]
    public void The_optiscaler_builds_have_one_row_per_page() =>
        Assert.HasCount(5, ComponentCatalog.All.Where(entry => entry.Id == "optiscaler").ToList());
}
