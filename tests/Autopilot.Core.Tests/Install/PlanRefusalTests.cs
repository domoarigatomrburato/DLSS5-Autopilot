using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>The refusals the folder's record and the plan itself decide, per install.md's "Refusal checks".</summary>
[TestClass]
public sealed class PlanRefusalTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_record_of_another_game_folder_says_to_uninstall_from_there()
    {
        using RefusalBed bed = new(GameFolders.Empty);
        Recorded.Plant(bed.Tree.PathOf("Sub"), Recorded.Install("Sub"));

        InstallCheck check = await bed.CheckAsync(InFolder(bed.Request(RouteId.Native), bed.Tree.PathOf("Sub")), Ct);

        check.AssertSays(BlockerKind.Record, "Uninstall from there first, then install here.");
    }

    [TestMethod]
    public async Task A_plan_that_contradicts_itself_is_stopped_and_asks_for_the_log()
    {
        using RefusalBed bed = new(GameFolders.Empty);

        InstallCheck check = await bed.CheckAsync(bed.Request(bed.Bed.Plan(RouteId.Native, new KeepEntry("dxgi.dll"))), Ct);

        check.AssertSays(BlockerKind.Plan, "Please report it with the log.");
    }

    // The request installing into folder, read as the game folder too.
    private static RefusalRequest InFolder(RefusalRequest request, string folder) => request with { Plan = request.Plan with { GameFolder = folder, InstallFolder = folder } };
}
