using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>The refusals an RTX Remix game decides, per remix.md and routes.md's "Pre-install checks and warnings".</summary>
[TestClass]
public sealed class RemixRefusalTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_remix_game_refuses_every_other_route()
    {
        using RefusalBed bed = new(GameFolders.Remix(RtxConf.FinalNewline));

        InstallCheck check = await bed.CheckAsync(bed.Request(RouteId.Native) with { Facts = RemixGame(bed) }, Ct);

        check.AssertSays(BlockerKind.RemixOnly, "Choose the remix route.");
    }

    [TestMethod]
    public async Task The_remix_route_needs_a_runtime()
    {
        using RefusalBed bed = new(GameFolders.Remix(RtxConf.FinalNewline));

        (await bed.CheckAsync(bed.Request(RouteId.Remix), Ct)).AssertSays(BlockerKind.NoRemixRuntime, "No RTX Remix runtime");
    }

    [TestMethod]
    public async Task Nvidias_own_remix_runtime_needs_the_swap()
    {
        using RefusalBed bed = new(GameFolders.Remix(RtxConf.FinalNewline));

        InstallCheck kept = await bed.CheckAsync(bed.Request(RouteId.Remix) with { Facts = RemixGame(bed) }, Ct);
        InstallCheck swapped = await bed.CheckAsync(bed.Request(RouteId.Remix, new RouteSettings { SwapRemixRuntime = true }) with { Facts = RemixGame(bed) }, Ct);

        kept.AssertSays(BlockerKind.RemixWithoutNeuralPass, "Turn on 'swap the Remix runtime'");
        swapped.AssertAllowed();
    }

    [TestMethod]
    public async Task A_runtime_folder_without_its_d3d9_names_the_missing_file()
    {
        using RefusalBed bed = new(TreeFile.Text(@".trex\readme.txt", "no runtime"));

        InstallCheck check = await bed.CheckAsync(bed.Request(RouteId.Remix) with { Facts = RemixGame(bed) }, Ct);

        check.AssertSays(BlockerKind.RemixRuntimeUnreadable, "d3d9.dll is missing");
        Assert.DoesNotContain(nameof(BlockerKind.RemixWithoutNeuralPass), check.Kinds());
    }

    [TestMethod]
    public async Task A_runtime_that_cannot_be_opened_names_the_file_instead_of_asking_for_the_swap()
    {
        using RefusalBed bed = new(TreeFile.Bytes(@".trex\d3d9.dll", GameFolders.Dll()));
        using FileStream held = bed.Lock(@".trex\d3d9.dll");

        InstallCheck check = await bed.CheckAsync(bed.Request(RouteId.Remix) with { Facts = RemixGame(bed) }, Ct);

        check.AssertSays(BlockerKind.RemixRuntimeUnreadable, "d3d9.dll could not be opened");
        Assert.DoesNotContain(nameof(BlockerKind.RemixWithoutNeuralPass), check.Kinds());
    }

    [TestMethod]
    [DataRow("rtx.neuralRendering")]
    [DataRow("rtx.neuralUplift")]
    public async Task A_runtime_with_the_neural_pass_needs_no_swap_wherever_its_marker_lies(string marker)
    {
        // Past the first 4 MiB chunk, across its edge.
        byte[] runtime = [.. new byte[(4 * 1024 * 1024) - 5], .. System.Text.Encoding.ASCII.GetBytes(marker)];
        using RefusalBed bed = new(TreeFile.Bytes(@".trex\d3d9.dll", runtime));

        (await bed.CheckAsync(bed.Request(RouteId.Remix) with { Facts = RemixGame(bed) }, Ct)).AssertAllowed();
    }

    private static RouteFacts RemixGame(RefusalBed bed) =>
        RefusalGames.Facts with { Api = GraphicsApi.DX9, Runtimes = RefusalGames.Facts.Runtimes with { RemixRuntime = bed.Tree.PathOf(".trex") } };
}
