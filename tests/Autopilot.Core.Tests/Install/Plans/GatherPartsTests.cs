using Autopilot.Core.Install;
using Autopilot.Core.Install.Plans;
using Autopilot.Core.Net;
using Autopilot.Core.Tests.Components.Fetch;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install.Plans;

/// <summary>How a gather takes each file's facts, and the parts of a gather the routes written so far do not reach yet.</summary>
[TestClass]
public sealed class GatherPartsTests(TestContext testContext)
{
    // A digest no test file hashes to: facts that carry it were not hashed.
    private const string PinnedDigest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [TestMethod]
    public async Task On_Auto_a_pinned_build_whose_pin_excludes_the_card_is_skipped_without_being_downloaded()
    {
        using GatherBed bed = new GatherBed(testContext).UsualMirror();

        GatheredFiles files = await bed.GatherAsync(facts: GatherFiles.Rtx30Game);

        Assert.AreEqual("310.8.0", files.Versions["dlssnr"]);
        Assert.AreEqual(0, bed.RequestsForMirror(GatherBed.RtxFortyTag));
        Assert.IsTrue(files.Notes.Any(note => note.Contains("310.8.0-RTX40", StringComparison.Ordinal)), string.Join(" | ", files.Notes));
    }

    [TestMethod]
    public async Task The_games_own_nvngx_dlss_kept_beside_the_executable_is_neither_fetched_nor_recorded()
    {
        using GatherBed bed = new GatherBed(testContext).UsualMirror();

        GatheredFiles files = await bed.GatherAsync(facts: GatherFiles.Rtx50Game with { DlssBesideExecutable = true });

        Assert.IsFalse(files.Versions.ContainsKey("dlss"));
        Assert.AreEqual(0, bed.RequestsForMirror("dlss-310.9.16"));
    }

    [TestMethod]
    public async Task A_file_whose_contents_check_pinned_its_digest_is_not_hashed_again()
    {
        using GameTree folder = new("pinned");
        string path = Path.Join(folder.Root, "nvngx_dlssnr.dll");
        await File.WriteAllBytesAsync(path, GatherFiles.Dll("pinned"), testContext.CancellationToken);

        FileFacts pinned = await BuildFetch.FactsAsync(new Sha256Verification(PinnedDigest), path, testContext.CancellationToken);
        FileFacts hashed = await BuildFetch.FactsAsync(null, path, testContext.CancellationToken);

        Assert.AreEqual(new FileFacts(PinnedDigest, GatherFiles.Dll("pinned").Length), pinned);
        Assert.AreEqual(FileFacts.Of(GatherFiles.Dll("pinned")), hashed);
    }

    [TestMethod]
    public async Task A_layer_manifest_is_read_once_for_its_bytes_and_its_facts()
    {
        using GatherBed bed = new GatherBed(testContext).UsualMirror();

        GatheredFiles files = await bed.GatherAsync();

        Assert.AreEqual(FileFacts.Of("{}"u8), files.Of("reshade", "ReShade64.json").Facts);
    }

    [TestMethod]
    public async Task A_component_its_source_resolves_is_gathered_with_every_file_its_version_and_where_it_came_from()
    {
        using GatherBed bed = new GatherBed(testContext).Bridge();

        GatheredFiles files = await bed.GatherComponentAsync("bridge");

        Assert.AreEqual("v2.0.0", files.Versions["bridge"]);
        Assert.AreEqual(FileFacts.Of(FetchFixtures.Addon64), files.Of("bridge", "dlss5-bridge.addon64").Facts);
    }
}
