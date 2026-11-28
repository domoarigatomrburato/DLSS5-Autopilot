using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>A file placed from the download cache is copied in, and the cached file stays.</summary>
[TestClass]
public sealed class CachedSourceTests(TestContext testContext)
{
    private static readonly byte[] _ours = GameFolders.Dll("cached");

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_file_from_the_cache_is_copied_into_the_game_folder()
    {
        using TransactionBed bed = new(GameFolders.Empty);
        using var cache = GameTree.Of(TreeFile.Bytes("a.dll", _ours));

        _ = await bed.CommitAsync(FromCache(bed, cache.PathOf("a.dll")), Ct);

        Assert.IsTrue(bed.Holds("a.dll", _ours) && File.Exists(cache.PathOf("a.dll")));
    }

    private static InstallPlan FromCache(TransactionBed bed, string cached) =>
        bed.Plan(RouteId.Native, new PlaceFile("a.dll", EntryClass.Component, PlanBeds.Facts(_ours), new CachedSource(cached)));
}
