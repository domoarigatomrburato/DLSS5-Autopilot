using System.Net;
using System.Text.Json.Nodes;
using Autopilot.Core.Components;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.Sources;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Components.Sources;

[TestClass]
public sealed class FeederTests(TestContext testContext)
{
    private const string EveryTestNotice =
        "Every recent DLSS5-Feeder release is a test build; v1.16.0-beta.6 was installed. Pick an exact version under 'feeder build' to change that.";

    private static readonly string[] _looseFiles = ["dlss5-feed.addon64", "dlss5-feed-host64.exe", "DLSS5_Feed.fx"];

    private static readonly string _latest = ComponentsBed.Api(ComponentRepositories.Feeder, "/latest");

    private static readonly string _list = ComponentsBed.ApiList(ComponentRepositories.Feeder, 15);

    [TestMethod]
    public async Task The_newest_release_is_taken_with_its_zip_and_the_digest_github_gives()
    {
        using ComponentsBed bed = new(testContext);
        bed.Fixture(_list, "feeder-releases.json");

        FeederRelease release = await bed.Feeder.ResolveAsync(FeederChoice.NewestRelease, bed.Cancellation);

        Assert.AreEqual("v2.0.0", release.Tag);
        Assert.IsNotNull(release.Package);
        Assert.AreEqual("DLSS5-Feeder-2.0.0.zip", release.Package.Name, "the install script and the notes beside it are not the feeder");
        Assert.IsEmpty(release.LooseFiles);
        Assert.IsNull(release.Notice);
        Assert.IsFalse(release.IsTestBuild);
        Assert.AreEqual(ReleaseOrigin.Api, release.Source.Origin);
        Assert.IsNotNull(release.Package.Digest);
        Assert.AreSame(release.Package.Digest, FeederRelease.VerificationOf(release.Package));
    }

    [TestMethod]
    public async Task Betas_published_without_the_pre_release_flag_are_not_the_newest_release()
    {
        using ComponentsBed bed = new(testContext);
        bed.Json(_list, FeederFixtures.BetasOverStable());

        FeederRelease release = await bed.Feeder.ResolveAsync(FeederChoice.NewestRelease, bed.Cancellation);

        Assert.AreEqual("v0.15.1", release.Tag, "six betas with the flag off, and a release flagged pre-release, sit above it");
        Assert.AreEqual(0, bed.Server.RequestsTo(_latest), "GitHub's own latest would answer the beta");
    }

    [TestMethod]
    public async Task The_newest_pre_release_is_the_newest_build_of_any_kind_but_a_draft()
    {
        using ComponentsBed bed = new(testContext);
        bed.Json(_list, FeederFixtures.BetasOverStable());

        FeederRelease release = await bed.Feeder.ResolveAsync(FeederChoice.NewestPrerelease, bed.Cancellation);

        Assert.AreEqual("v1.16.0-beta.6", release.Tag);
        Assert.IsTrue(release.IsTestBuild);
        Assert.IsNull(release.Notice, "a test build was asked for");
    }

    [TestMethod]
    public async Task An_exact_tag_among_the_newest_fifteen_is_taken_with_its_loose_files()
    {
        using ComponentsBed bed = new(testContext);
        bed.Json(_list, FeederFixtures.BetasOverStable());

        FeederRelease release = await bed.Feeder.ResolveAsync(FeederChoice.Exact("v0.9.2"), bed.Cancellation);

        Assert.IsNull(release.Package);
        CollectionAssert.AreEqual(_looseFiles, ReleaseFixtures.AssetNames(release.LooseFiles), "the readme is not a feeder file");
    }

    [TestMethod]
    public void A_file_without_a_digest_is_checked_by_its_magic_bytes_only() =>
        Assert.AreEqual(
            new NoVerification("jlrouzies-fr/DLSS5-Feeder: the release data gave no digest"),
            FeederRelease.VerificationOf(new("DLSS5_Feed.fx", new(ComponentsBed.Download(FeederFixtures.Repo, "v0.9.2", "DLSS5_Feed.fx")), null)));

    [TestMethod]
    public async Task An_exact_tag_that_is_not_listed_is_refused_by_name()
    {
        using ComponentsBed bed = new(testContext);
        bed.Json(_list, FeederFixtures.BetasOverStable());

        ComponentException failure = await Assert.ThrowsAsync<ComponentException>(() => bed.Feeder.ResolveAsync(FeederChoice.Exact("v0.7.0"), bed.Cancellation));

        Assert.AreEqual(ComponentFailure.TagNotListed, failure.Reason);
        Assert.AreEqual("DLSS5-Feeder release v0.7.0 is not on GitHub's release list (the newest 15 are checked).", failure.Message);
    }

    [TestMethod]
    public async Task With_only_test_builds_among_the_fifteen_the_release_pages_are_walked_for_a_release()
    {
        using ComponentsBed bed = new(testContext);
        bed.Json(_list, FeederFixtures.OnlyBetas())
            .Page(1, "v1.16.0-beta.6", "v1.16.0-beta.5")
            .Page(2, "v1.16.0-beta.1", "v0.15.1", "v0.15.0")
            .Json(ComponentsBed.Api(FeederFixtures.Repo, "/tags/v0.15.1"), FeederFixtures.Release("v0.15.1", 9, "DLSS5-Feeder-0.15.1.zip"));

        FeederRelease release = await bed.Feeder.ResolveAsync(FeederChoice.NewestRelease, bed.Cancellation);

        Assert.AreEqual("v0.15.1", release.Tag);
        Assert.IsNull(release.Notice);
        Assert.AreEqual(0, ReleaseFixtures.AssetPageRequests(bed.Server), "the walk reads tags, not each release");
    }

    [TestMethod]
    public async Task When_every_source_answers_a_test_build_it_is_installed_and_said()
    {
        using ComponentsBed bed = new(testContext);
        bed.Json(_list, FeederFixtures.OnlyBetas())
            .Page(1, "v1.16.0-beta.6", "v1.16.0-beta.5")
            .Page(2, "v1.16.0-beta.4")
            .Json(_latest, FeederFixtures.Release("v1.16.0-beta.6", 0, "DLSS5-Feeder-1.16.0-beta.6.zip"));

        FeederRelease release = await bed.Feeder.ResolveAsync(FeederChoice.NewestRelease, bed.Cancellation);

        Assert.AreEqual("v1.16.0-beta.6", release.Tag);
        Assert.AreEqual(EveryTestNotice, release.Notice);
        Assert.AreEqual(1, bed.Server.RequestsTo(ComponentsBed.ListPage(FeederFixtures.Repo, 2)));
    }

    [TestMethod]
    public async Task With_no_latest_release_and_the_pages_out_of_reach_the_newest_test_build_is_installed_and_said()
    {
        using ComponentsBed bed = new(testContext);
        bed.Json(_list, FeederFixtures.OnlyBetas()).Unreachable(ComponentsBed.ListPage(FeederFixtures.Repo, 1));
        bed.Server.Answer(_latest, HttpStatusCode.NotFound, "{}");

        FeederRelease release = await bed.Feeder.ResolveAsync(FeederChoice.NewestRelease, bed.Cancellation);

        Assert.AreEqual("v1.16.0-beta.6", release.Tag);
        Assert.AreEqual(EveryTestNotice, release.Notice);
    }

    [TestMethod]
    public async Task A_release_without_the_feeder_files_is_refused()
    {
        using ComponentsBed bed = new(testContext);
        bed.Json(_list, FeederFixtures.WithoutFiles());

        ComponentException failure = await Assert.ThrowsAsync<ComponentException>(() => bed.Feeder.ResolveAsync(FeederChoice.NewestRelease, bed.Cancellation));

        Assert.AreEqual(ComponentFailure.NoFiles, failure.Reason);
        Assert.AreEqual("DLSS5-Feeder release v2.1.0 carries neither the feeder's zip nor its files. Pick another version under 'feeder build'.", failure.Message);
    }

    [TestMethod]
    public async Task A_repository_without_releases_has_no_newest_build()
    {
        using ComponentsBed bed = new(testContext);
        bed.Json(_list, ReleaseJsonText.List());

        ComponentException failure = await Assert.ThrowsAsync<ComponentException>(() => bed.Feeder.ResolveAsync(FeederChoice.NewestPrerelease, bed.Cancellation));

        Assert.AreEqual(ComponentFailure.NoRelease, failure.Reason);
    }

    [TestMethod]
    public async Task Each_choice_is_resolved_once_per_run()
    {
        using ComponentsBed bed = new(testContext);
        bed.Json(_list, FeederFixtures.BetasOverStable());

        FeederRelease newest = await bed.Feeder.ResolveAsync(FeederChoice.NewestRelease, bed.Cancellation);
        FeederRelease again = await bed.Feeder.ResolveAsync(FeederChoice.NewestRelease, bed.Cancellation);
        FeederRelease exact = await bed.Feeder.ResolveAsync(FeederChoice.Exact("v0.15.1"), bed.Cancellation);
        FeederRelease exactAgain = await bed.Feeder.ResolveAsync(FeederChoice.Exact("v0.15.1"), bed.Cancellation);

        Assert.AreSame(newest, again);
        Assert.AreSame(exact, exactAgain);
        Assert.AreNotSame(newest, exact, "each choice is its own resolution");
        Assert.AreEqual(1, bed.Server.RequestsTo(_list), "the API answer itself is kept by the network cache");
    }

    [TestMethod]
    public void An_exact_choice_needs_a_tag() =>
        Assert.ThrowsExactly<ArgumentException>(() => FeederChoice.Exact(" "));

    [TestMethod]
    public void A_test_build_is_told_by_the_flag_or_by_its_tag()
    {
        Assert.IsTrue(FeederSource.IsTestBuild(new("v1.0.0", Prerelease: true, Draft: false, null, [])));
        Assert.IsTrue(FeederSource.IsTestBuild(new("v1.16.0-beta.2", Prerelease: false, Draft: false, null, [])));
        Assert.IsTrue(FeederSource.IsTestBuild(new("v2.0.0-rc1", Prerelease: null, Draft: null, null, [])));
        Assert.IsFalse(FeederSource.IsTestBuild(new("v0.15.1", Prerelease: null, Draft: null, null, [])));
    }
}

/// <summary>Feeder release lists written for the cases #325 and #348 describe, and its github.com list pages.</summary>
internal static class FeederFixtures
{
    public const string Repo = ComponentRepositories.Feeder;

    public static JsonObject Release(string tag, int age, params string[] assets) => ReleaseJsonText.Release(Repo, tag, age, assets);

    // #325 and #348: six betas published with the flag off above the release line, a release flagged
    // pre-release, a draft on top, and an old release of loose files.
    public static JsonArray BetasOverStable()
    {
        JsonObject draft = Release("v1.17.0", 0, "DLSS5-Feeder-1.17.0.zip");
        draft["draft"] = true;
        JsonArray list = [draft];
        for (int beta = 6; beta >= 1; beta--)
        {
            list.Add(Release($"v1.16.0-beta.{beta}", 7 - beta, $"DLSS5-Feeder-1.16.0-beta.{beta}.zip"));
        }

        list.Add(ReleaseJsonText.Release(Repo, "v1.0.0", prerelease: true, 7, "DLSS5-Feeder-1.0.0.zip"));
        list.Add(Release("v0.15.1", 8, "DLSS5-Feeder-0.15.1.zip"));
        list.Add(Release("v0.9.2", 30, "dlss5-feed.addon64", "dlss5-feed-host64.exe", "DLSS5_Feed.fx", "README.md"));
        return list;
    }

    public static JsonArray WithoutFiles() => [Release("v2.1.0", 0, "Install-DLSS5Feeder.ps1", "Tools.zip")];

    public static JsonArray OnlyBetas()
    {
        JsonArray list = [];
        for (int beta = 6; beta >= 1; beta--)
        {
            list.Add(Release($"v1.16.0-beta.{beta}", 6 - beta, $"DLSS5-Feeder-1.16.0-beta.{beta}.zip"));
        }

        return list;
    }

    /// <summary>The feeder's release list page <paramref name="page"/>, in github.com's markup, listing <paramref name="tags"/>.</summary>
    public static ComponentsBed Page(this ComponentsBed bed, int page, params string[] tags)
    {
        bed.Server.Answer(
            ComponentsBed.ListPage(Repo, page),
            HttpStatusCode.OK,
            ReleaseFixtures.ListPageWith(tags).Replace("doitsujin/dxvk", ComponentRepositories.Feeder, StringComparison.Ordinal));
        return bed;
    }
}
