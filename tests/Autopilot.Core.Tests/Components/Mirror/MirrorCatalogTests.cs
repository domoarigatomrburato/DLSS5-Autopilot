using System.Net;
using Autopilot.Core.Components.Mirror;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Components.Mirror;

[TestClass]
public sealed class MirrorCatalogTests(TestContext testContext)
{
    private const string PagesNotice = "GitHub's API could not be reached; this release was read from github.com's release pages instead.";
    private const string NvidiaLabel = "310.9.16 (NVIDIA SDK)";
    private const string NvidiaListNote = "NVIDIA's list could not be read; the mirror's copy, signed by NVIDIA, was used";

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

        // Builds whose versions tie (310.8.SF, 310.8.Lecram, 310.8.0) go by tag, ordinal, descending.
        Assert.AreEqual("310.9.16-nvidia, 310.9.16, 310.8.SF-v2, 310.8.0-RTX40, 310.8.SF, 310.8.Lecram, 310.8.0", MirrorBed.Labels(catalog, BuildFamily.Dlssnr));
        Assert.AreEqual(NvidiaLabel + ", 310.9.16, 310.9.1, 310.9.0, 310.8.0, 310.7.129, 310.7.128", MirrorBed.Labels(catalog, BuildFamily.Dlss));
        Assert.AreEqual(NvidiaLabel + ", 310.9.16, 310.9.1, 310.9.0, 310.7.129, 310.7.128", MirrorBed.Labels(catalog, BuildFamily.Dlssd));
        Assert.IsTrue(catalog.Entries(BuildFamily.Dlssg).Skip(1).All(entry => entry.Tag.StartsWith("dlssg-", StringComparison.Ordinal)));
        Assert.HasCount(18, catalog.Entries(BuildFamily.Dlssg).Skip(1).ToList());
    }

    [TestMethod]
    public async Task The_order_is_the_same_whatever_order_the_list_comes_in()
    {
        BuildCatalog captured = await MirrorBed.Captured(testContext);
        BuildCatalog reversed = await Reversed();

        foreach (BuildFamily family in Enum.GetValues<BuildFamily>())
        {
            Assert.AreEqual(MirrorBed.Labels(captured, family), MirrorBed.Labels(reversed, family), family.ToString());
        }

        Assert.AreEqual("310.9.16-nvidia, 310.9.16, 310.8.SF-v2, 310.8.0-RTX40, 310.8.SF, 310.8.Lecram, 310.8.0", MirrorBed.Labels(reversed, BuildFamily.Dlssnr));
    }

    [TestMethod]
    public async Task Nvidias_build_stays_ahead_of_the_community_build_with_the_same_numbers()
    {
        foreach (BuildCatalog catalog in new[] { await MirrorBed.Captured(testContext), await Reversed() })
        {
            Assert.AreEqual("310.9.16-nvidia, 310.9.16", MirrorBed.Labels(catalog.Entries(BuildFamily.Dlssnr).Take(2)));
        }
    }

    [TestMethod]
    public async Task Releases_of_no_family_are_left_out()
    {
        BuildCatalog catalog = await MirrorBed.Captured(testContext);

        string[] tags = [.. Enum.GetValues<BuildFamily>().SelectMany(catalog.Entries).Select(entry => entry.Tag)];
        Assert.IsFalse(tags.Any(tag => tag.StartsWith("streamline", StringComparison.Ordinal) || tag.StartsWith("DLSS-Enabler", StringComparison.Ordinal) || tag.StartsWith("ue-dof-fix", StringComparison.Ordinal)));
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
    [DataRow("310.8.0")]
    [DataRow("310.9.16-nvidia")]
    public async Task Nvidias_own_dlssnr_build_must_carry_nvidias_signature(string label)
    {
        BuildCatalog catalog = await MirrorBed.Captured(testContext);

        BuildEntry entry = catalog.Entries(BuildFamily.Dlssnr).Single(entry => entry.Label == label);

        Assert.AreEqual(new SignerVerification("NVIDIA Corporation"), entry.Contents);
        Assert.IsInstanceOfType<Sha256Verification>(entry.Download);
    }

    [TestMethod]
    [DataRow("310.8.0-RTX40", "4b8d19bc3eff58a084f5eca7489c921501c203450169fb82ff4f649a4482ba05")]
    [DataRow("310.8.SF", "4c5bd1171c7336b4b04fb394de51da285ab6ead6f922d7afdec163f71c319d74")]
    [DataRow("310.8.SF-v2", "6eb209e764f39872625debd6abaf45e2bb6322f6f270f781f70c059ae30b3927")]
    [DataRow("310.8.Lecram", "f95feb54137ea11979f9b4ec4f00afd84b5c98a5624d3388fbf6a87714a39fcc")]
    [DataRow("310.9.16", "cdfa6c1ec784867182164c7836f324f087b5c9f374d0c9fe46f54999feabf3ed")]
    public async Task A_community_dlssnr_build_must_match_the_digest_pinned_for_its_label(string label, string digest)
    {
        BuildCatalog catalog = await MirrorBed.Captured(testContext);

        BuildEntry entry = catalog.Entries(BuildFamily.Dlssnr).Single(entry => entry.Label == label);

        Assert.AreEqual(new Sha256Verification(digest), entry.Contents);
        Assert.IsInstanceOfType<Sha256Verification>(entry.Download, "the zip by its release digest");
    }

    [TestMethod]
    [DataRow(BuildFamily.Dlss, "dlss-310.9.16")]
    [DataRow(BuildFamily.Dlssd, "dlssd-310.9.16")]
    [DataRow(BuildFamily.Dlssg, "dlssg-310.9.16")]
    public async Task Nvidias_runtimes_on_the_mirror_must_carry_nvidias_signature_too(BuildFamily family, string tag)
    {
        BuildCatalog catalog = await MirrorBed.Captured(testContext);

        BuildEntry entry = catalog.Entries(family).Single(entry => entry.Tag == tag);

        Assert.AreEqual(BuildPublisher.Mirror, entry.Publisher);
        Assert.AreEqual(new SignerVerification("NVIDIA Corporation"), entry.Contents);
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

        Assert.HasCount(6, catalog.Entries(BuildFamily.Dlssd));
        Assert.IsNull(catalog.Source.Notice);
        Assert.IsFalse(catalog.NvidiaListUnread);
        Assert.IsNull(catalog.NvidiaListNote);
    }

    [TestMethod]
    public async Task Nvidias_tag_costs_one_head_and_no_api_request()
    {
        using MirrorBed bed = new(testContext);
        _ = await bed.ApiAnswers().NvidiaRedirects().Get();

        CollectionAssert.AreEqual(new[] { HttpMethod.Head }, bed.Server.MethodsTo(MirrorBed.NvidiaLatest));
        Assert.IsFalse(bed.Server.Requests.Any(request => request.Url.AbsolutePath.StartsWith("/repos/NVIDIA", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task The_catalog_is_remembered_for_the_run()
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
        Assert.AreEqual("dlssd-310.9.16", first.Entries(BuildFamily.Dlssd)[0].Tag, "ray reconstruction too");
        Assert.IsTrue(first.NvidiaListUnread);
        Assert.AreEqual(NvidiaListNote, first.NvidiaListNote);
        Assert.AreEqual(NvidiaLabel, second.Entries(BuildFamily.Dlss)[0].Label);
        Assert.IsFalse(second.NvidiaListUnread, "the failure is not remembered");
        Assert.AreEqual(1, bed.RequestsTo(MirrorBed.Api), "the mirror's list is remembered");
    }

    [TestMethod]
    [DataRow("https://evil.test/NVIDIA/DLSS/releases/tag/v310.9.16", DisplayName = "off the host list")]
    [DataRow("http://github.com/NVIDIA/DLSS/releases/tag/v310.9.16", DisplayName = "a downgrade to http")]
    [DataRow("https://github.com/NVIDIA/DLSS/releases", DisplayName = "no tag")]
    public async Task A_redirect_that_names_no_usable_tag_leaves_the_mirror_alone_and_says_so(string location)
    {
        using MirrorBed bed = new(testContext);
        bed.ApiAnswers().Server.Redirect(MirrorBed.NvidiaLatest, location);

        BuildCatalog catalog = await bed.Get();

        Assert.AreEqual("310.9.16, 310.9.1, 310.9.0, 310.7.129, 310.7.128", MirrorBed.Labels(catalog, BuildFamily.Dlssd), "the mirror alone serves ray reconstruction");
        Assert.AreEqual(BuildPublisher.Mirror, catalog.Entries(BuildFamily.Dlss)[0].Publisher);
        Assert.AreEqual(new SignerVerification("NVIDIA Corporation"), catalog.Entries(BuildFamily.Dlssg)[0].Contents);
        Assert.IsTrue(catalog.NvidiaListUnread);
        Assert.AreEqual(NvidiaListNote, catalog.NvidiaListNote);
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
        Assert.AreEqual(NvidiaLabel + ", 310.9.16, 310.9.1, 310.9.0", MirrorBed.Labels(catalog, BuildFamily.Dlssd));
        Assert.AreEqual(PagesNotice, catalog.Source.Notice);
    }

    [TestMethod]
    public async Task The_pages_walk_reads_only_the_builds_it_keeps()
    {
        using MirrorBed bed = PagesBed();

        _ = await bed.Get();

        foreach (string skipped in new[] { "renodx-dlss5-8.5.0-rc10", "dlssnr-310.8.SF-v2", "renodx-dlss-SF-26.0928.0205", "renodx-dlss5-7.0.0-rc8", "dlssd-310.7.129", "streamline-2.14.16.0", "DLSS-Enabler-4.10.0.27" })
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

    // The captured list, oldest first.
    private async Task<BuildCatalog> Reversed()
    {
        using MirrorBed bed = new(testContext);
        bed.NvidiaRedirects().Server.Answer(MirrorBed.Api, HttpStatusCode.OK, MirrorFixtures.ApiListReversed());
        return await bed.Get();
    }

    // The API is unreachable (first, when a test adds an answer after it); page 1 is the captured one, pages 2 and 3 carry older builds, 4.55 is on none of them.
    private MirrorBed PagesBed()
    {
        MirrorBed bed = new(testContext);
        bed.Unreachable(MirrorBed.Api).NvidiaRedirects().Pages(
            MirrorFixtures.PageTags,
            ["renodx-dlss5-8.5.0-rc5", "renodx-dlss5-8.5.0-rc4", "renodx-dlss5-8.5.0-rc10", "dlssnr-310.8.Lecram", "dlssnr-310.8.SF-v2", "renodx-dlss-SF-26.1003.2350", "renodx-dlss-SF-26.0928.0205", "dlssg-310.9.16", "dlssd-310.9.16", "dlss-310.9.16"],
            ["renodx-dlss5-7.0.0-rc8", "renodx-dlss5-4.60", "dlssg-310.9.1", "dlssd-310.9.1", "dlss-310.9.1", "dlssg-310.9.0", "dlssd-310.9.0", "dlss-310.9.0", "dlssd-310.7.129"]);
        bed.Server.Answer(MirrorBed.Expanded("renodx-dlss5-4.55"), HttpStatusCode.OK, ReleaseFixtures.Fixture("rhi-repo-expanded-assets-renodx-dlss5-4.55.html"));
        return bed;
    }
}
