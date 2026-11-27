using System.Net;
using Autopilot.Core.Components;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Components;

[TestClass]
public sealed class MirrorCatalogTests(TestContext testContext)
{
    private const string PagesNotice = "GitHub's API could not be reached; this release was read from github.com's release pages instead.";
    private const string NvidiaLabel = "310.9.16 (NVIDIA SDK)";

    [TestMethod]
    public async Task Each_family_lists_its_builds_newest_first()
    {
        BuildCatalog catalog = await MirrorBed.Captured(testContext);

        Assert.AreEqual("10.8.0, 8.5.0-rc10, 8.5.0-rc5, 8.5.0-rc4, 7.0.0-rc8, 7.0.0-rc1, 6.5.3, 6.4.1, 5.2.1, 4.70, 4.60, 4.55, 4.5, 3.3.4, 3.3.1, 2.5", MirrorBed.Labels(catalog, BuildFamily.Renodx));
        Assert.AreEqual("26.1025.0009, 26.1016.1304, 26.1003.2350, 26.0928.0205, 26.0927.2125, 26.0922.0041, 26.0919.2025, 26.0917.1904, 26.0917.1436, 26.0917.1145, 0.55, 0.54, 0.53, 0.52, 0.51, 0.5, 0.4, 0.3, 0.2, 0.1", MirrorBed.Labels(catalog, BuildFamily.RenodxSf));
    }

    [TestMethod]
    public async Task A_tag_belongs_to_the_family_of_its_longest_prefix()
    {
        BuildCatalog catalog = await MirrorBed.Captured(testContext);

        // Builds that sort as equal (310.8.Lecram, 310.8.SF, 310.8.0) keep the list's order.
        Assert.AreEqual("310.9.16-nvidia, 310.9.16, 310.8.SF-v2, 310.8.0-RTX40, 310.8.Lecram, 310.8.SF, 310.8.0", MirrorBed.Labels(catalog, BuildFamily.Dlssnr));
        Assert.AreEqual(NvidiaLabel + ", 310.9.16, 310.9.1, 310.9.0, 310.8.0, 310.7.129, 310.7.128", MirrorBed.Labels(catalog, BuildFamily.Dlss));
        Assert.IsTrue(catalog.Entries(BuildFamily.Dlssg).Skip(1).All(entry => entry.Tag.StartsWith("dlssg-", StringComparison.Ordinal)));
        Assert.HasCount(18, catalog.Entries(BuildFamily.Dlssg).Skip(1).ToList());
    }

    [TestMethod]
    public async Task Releases_of_no_family_are_left_out()
    {
        BuildCatalog catalog = await MirrorBed.Captured(testContext);

        string[] tags = [.. Enum.GetValues<BuildFamily>().SelectMany(catalog.Entries).Select(entry => entry.Tag)];
        Assert.IsFalse(tags.Any(tag => tag.StartsWith("streamline", StringComparison.Ordinal) || tag.StartsWith("DLSS-Enabler", StringComparison.Ordinal) || tag.StartsWith("ue-dof-fix", StringComparison.Ordinal)));
        Assert.IsFalse(tags.Any(tag => tag.StartsWith("dlssd-", StringComparison.Ordinal)), "the mirror's dlssd releases are not read");
    }

    [TestMethod]
    public async Task A_mirror_entry_is_its_release_zip_checked_by_its_digest()
    {
        BuildCatalog catalog = await MirrorBed.Captured(testContext);

        BuildEntry entry = catalog.Entries(BuildFamily.Renodx).Single(entry => entry.Label == "4.55");

        Assert.AreEqual("renodx-dlss5-4.55", entry.Tag);
        Assert.AreEqual("renodx-dlss5_4.55.zip", entry.FileName);
        Assert.AreEqual(new Uri("https://github.com/RankFTW/rhi-repo/releases/download/renodx-dlss5-4.55/renodx-dlss5_4.55.zip"), entry.Url);
        Assert.AreEqual(new Sha256Verification(MirrorFixtures.Renodx455Digest), entry.Download);
        Assert.AreEqual(BuildPublisher.Mirror, entry.Publisher);
        Assert.IsFalse(entry.IsRaw);
        Assert.IsNull(entry.Contents);
    }

    [TestMethod]
    public async Task A_mirror_runtime_must_carry_nvidias_signature()
    {
        BuildCatalog catalog = await MirrorBed.Captured(testContext);

        BuildEntry entry = catalog.Entries(BuildFamily.Dlssnr).Single(entry => entry.Label == "310.8.0-RTX40");

        Assert.AreEqual(new SignerVerification("NVIDIA Corporation"), entry.Contents);
        Assert.IsInstanceOfType<Sha256Verification>(entry.Download);
    }

    [TestMethod]
    public async Task Nvidias_runtimes_come_first_read_from_its_sdk_tree_and_signed()
    {
        BuildCatalog catalog = await MirrorBed.Captured(testContext);

        foreach ((BuildFamily family, string name) in new[] { (BuildFamily.Dlss, "nvngx_dlss.dll"), (BuildFamily.Dlssd, "nvngx_dlssd.dll"), (BuildFamily.Dlssg, "nvngx_dlssg.dll") })
        {
            BuildEntry first = catalog.Entries(family)[0];
            Assert.AreEqual(NvidiaLabel, first.Label);
            Assert.AreEqual("v310.9.16", first.Tag);
            Assert.AreEqual(new Uri($"https://raw.githubusercontent.com/NVIDIA/DLSS/v310.9.16/lib/Windows_x86_64/rel/{name}"), first.Url);
            Assert.AreEqual(name, first.FileName);
            Assert.AreEqual(new SignerVerification("NVIDIA Corporation"), first.Download);
            Assert.IsTrue(first.IsRaw);
        }

        Assert.ContainsSingle(catalog.Entries(BuildFamily.Dlssd));
        Assert.IsNull(catalog.Source.Notice);
    }

    [TestMethod]
    public async Task Nvidias_tag_costs_one_head_and_no_api_request()
    {
        using MirrorBed bed = new(testContext);
        _ = await bed.ApiAnswers().NvidiaRedirects().Get();

        CollectionAssert.AreEqual(new[] { HttpMethod.Head }, bed.MethodsTo(MirrorBed.NvidiaLatest));
        Assert.IsFalse(bed.Server.Requests.Any(request => request.Url.AbsolutePath.StartsWith("/repos/NVIDIA", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task The_catalog_is_remembered_for_the_process()
    {
        using MirrorBed bed = new(testContext);
        _ = await bed.ApiAnswers().NvidiaRedirects().Get();
        int requests = bed.Server.Requests.Count;

        BuildCatalog again = await bed.Get();

        Assert.HasCount(requests, bed.Server.Requests);
        Assert.AreEqual(NvidiaLabel, again.Entries(BuildFamily.Dlss)[0].Label);
    }

    [TestMethod]
    public async Task A_failed_nvidia_redirect_adds_nothing_and_is_asked_again()
    {
        using MirrorBed bed = new(testContext);
        bed.ApiAnswers().Unreachable(MirrorBed.NvidiaLatest).NvidiaRedirects();

        BuildCatalog first = await bed.Get();
        BuildCatalog second = await bed.Get();

        Assert.AreEqual("310.9.16", first.Entries(BuildFamily.Dlss)[0].Label, "the mirror alone serves the family");
        Assert.IsEmpty(first.Entries(BuildFamily.Dlssd));
        Assert.AreEqual(NvidiaLabel, second.Entries(BuildFamily.Dlss)[0].Label);
        Assert.AreEqual(1, bed.RequestsTo(MirrorBed.Api), "the mirror's list is remembered");
    }

    [TestMethod]
    [DataRow("https://evil.test/NVIDIA/DLSS/releases/tag/v310.9.16", DisplayName = "off the host list")]
    [DataRow("https://github.com/NVIDIA/DLSS/releases", DisplayName = "no tag")]
    public async Task A_redirect_that_names_no_usable_tag_adds_nothing(string location)
    {
        using MirrorBed bed = new(testContext);
        bed.ApiAnswers().Server.Redirect(MirrorBed.NvidiaLatest, location);

        BuildCatalog catalog = await bed.Get();

        Assert.AreEqual("310.9.16", catalog.Entries(BuildFamily.Dlss)[0].Label);
        Assert.AreEqual(BuildPublisher.Mirror, catalog.Entries(BuildFamily.Dlss)[0].Publisher);
    }

    [TestMethod]
    public async Task A_repository_the_api_does_not_know_is_a_failure()
    {
        using MirrorBed bed = new(testContext);
        bed.NvidiaRedirects().Server.Answer(MirrorBed.Api, HttpStatusCode.NotFound, /*lang=json,strict*/ """{"message":"Not Found"}""");

        NetException failure = await Assert.ThrowsAsync<NetException>(bed.Get);

        Assert.AreEqual(new HttpStatusDetail(HttpStatusCode.NotFound), failure.Detail);
    }

    [TestMethod]
    public async Task The_pages_give_three_builds_per_family_and_every_pinned_build()
    {
        using MirrorBed bed = PagesBed();

        BuildCatalog catalog = await bed.Get();

        Assert.AreEqual("10.8.0, 8.5.0-rc5, 8.5.0-rc4, 4.60, 4.55", MirrorBed.Labels(catalog, BuildFamily.Renodx));
        Assert.AreEqual("310.9.16-nvidia, 310.9.16, 310.8.Lecram", MirrorBed.Labels(catalog, BuildFamily.Dlssnr));
        Assert.AreEqual("26.1025.0009, 26.1016.1304, 26.1003.2350", MirrorBed.Labels(catalog, BuildFamily.RenodxSf));
        Assert.AreEqual(NvidiaLabel + ", 310.9.16, 310.9.1, 310.9.0", MirrorBed.Labels(catalog, BuildFamily.Dlss));
        Assert.AreEqual(PagesNotice, catalog.Source.Notice);
    }

    [TestMethod]
    public async Task The_pages_walk_reads_only_the_builds_it_keeps()
    {
        using MirrorBed bed = PagesBed();

        _ = await bed.Get();

        foreach (string skipped in new[] { "renodx-dlss5-8.5.0-rc10", "dlssnr-310.8.SF-v2", "renodx-dlss-SF-26.0928.0205", "renodx-dlss5-7.0.0-rc8", "streamline-2.14.16.0", "DLSS-Enabler-4.10.0.27" })
        {
            Assert.AreEqual(0, bed.RequestsTo(MirrorBed.Expanded(skipped)), skipped);
        }

        Assert.AreEqual(1, bed.RequestsTo(MirrorBed.Expanded("renodx-dlss5-4.60")), "a pinned build is kept past the count");
        Assert.AreEqual(1, bed.RequestsTo(MirrorBed.Expanded("renodx-dlss5-4.55")), "a pinned build the walk did not reach is asked by its tag");
        Assert.AreEqual(0, bed.RequestsTo(MirrorBed.ListPage(4)), "every family is full after page 3");
    }

    [TestMethod]
    public async Task A_build_read_from_the_pages_is_checked_by_the_digest_its_page_shows()
    {
        using MirrorBed bed = PagesBed();

        BuildCatalog catalog = await bed.Get();

        BuildEntry entry = catalog.Entries(BuildFamily.Renodx).Single(entry => entry.Label == "4.55");
        Assert.AreEqual(new Sha256Verification(MirrorFixtures.Renodx455Digest), entry.Download);
        Assert.AreEqual("renodx-dlss5_4.55.zip", entry.FileName);
    }

    [TestMethod]
    public async Task A_build_whose_page_shows_no_digest_is_used_unverified_beyond_its_content()
    {
        using MirrorBed bed = new(testContext);
        bed.Unreachable(MirrorBed.Api).NvidiaRedirects().Fragments(["renodx-dlss5-4.60", "renodx-dlss5-4.55"]);
        bed.Server.Answer(MirrorBed.ListPage(1), HttpStatusCode.OK, MirrorFixtures.ListPage("dlssnr-310.8.0"));
        bed.Server.Answer(MirrorBed.ListPage(2), HttpStatusCode.NotFound, "Not Found");
        bed.Server.Answer(MirrorBed.Expanded("dlssnr-310.8.0"), HttpStatusCode.OK, MirrorFixtures.ExpandedFor("dlssnr-310.8.0").Replace("sha256:", "md5:", StringComparison.Ordinal));

        BuildCatalog catalog = await bed.Get();

        Assert.IsInstanceOfType<NoVerification>(Assert.ContainsSingle(catalog.Entries(BuildFamily.Dlssnr)).Download);
    }

    [TestMethod]
    public async Task The_shortened_list_is_not_remembered()
    {
        using MirrorBed bed = PagesBed();
        bed.ApiAnswers();
        _ = await bed.Get();

        BuildCatalog again = await bed.Get();

        Assert.AreEqual(2, bed.RequestsTo(MirrorBed.Api));
        Assert.IsNull(again.Source.Notice);
        Assert.HasCount(16, again.Entries(BuildFamily.Renodx));
    }

    [TestMethod]
    public async Task When_the_pages_fail_too_the_api_failure_is_reported()
    {
        using MirrorBed bed = new(testContext);
        bed.Unreachable(MirrorBed.Api).NvidiaRedirects().Unreachable(MirrorBed.ListPage(1));

        NetException failure = await Assert.ThrowsAsync<NetException>(bed.Get);

        Assert.AreEqual("api.github.com", failure.Host);
    }

    // The API is unreachable (first, when a test adds an answer after it); page 1 is the captured one, pages 2 and 3 carry older builds, 4.55 is on none of them.
    private MirrorBed PagesBed()
    {
        MirrorBed bed = new(testContext);
        bed.Unreachable(MirrorBed.Api).NvidiaRedirects().Pages(
            MirrorFixtures.PageTags,
            ["renodx-dlss5-8.5.0-rc5", "renodx-dlss5-8.5.0-rc4", "renodx-dlss5-8.5.0-rc10", "dlssnr-310.8.Lecram", "dlssnr-310.8.SF-v2", "renodx-dlss-SF-26.1003.2350", "renodx-dlss-SF-26.0928.0205", "dlssg-310.9.16", "dlss-310.9.16"],
            ["renodx-dlss5-7.0.0-rc8", "renodx-dlss5-4.60", "dlssg-310.9.1", "dlss-310.9.1", "dlssg-310.9.0", "dlss-310.9.0"]);
        bed.Server.Answer(MirrorBed.Expanded("renodx-dlss5-4.55"), HttpStatusCode.OK, ReleaseFixtures.Fixture("rhi-repo-expanded-assets-renodx-dlss5-4.55.html"));
        return bed;
    }
}
