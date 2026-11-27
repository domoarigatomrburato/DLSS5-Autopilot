using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>The person's own files: frame generation files that cannot be placed, an own add-on missing or not 64-bit (routes.md, "Frame generation").</summary>
[TestClass]
public sealed class OwnFileRefusalTests(TestContext testContext)
{
    private static readonly RouteSettings _sm86 = new() { OwnFrameGeneration = OwnFrameGeneration.Sm86 };

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task Own_frame_generation_files_optiscaler_would_load_over_are_refused()
    {
        using RefusalBed bed = new();
        using GameTree store = Store(GameFolders.Dll("sm86"));

        InstallCheck check = await bed.CheckAsync(Stored(bed.Request(RouteId.OptiScaler, _sm86 with { OptiScalerLoadsAs = OptiScalerProxy.Version }), store), Ct);

        Assert.AreEqual("OptiScaler would load as version.dll here, the name dlssg_for_sm86 needs. Choose another name under 'OptiScaler loads as', then install again.", check.Only(BlockerKind.OwnFrameGeneration));
    }

    [TestMethod]
    public async Task Own_frame_generation_files_no_longer_stored_are_refused()
    {
        using RefusalBed bed = new();

        (await bed.CheckAsync(bed.Request(RouteId.OptiScaler, _sm86), Ct)).AssertSays(BlockerKind.OwnFrameGeneration, "Add them again under 'frame generation files'");
    }

    [TestMethod]
    public async Task A_foreign_file_under_their_name_is_refused()
    {
        using RefusalBed bed = new(TreeFile.Bytes("version.dll", GameFolders.Dll("another mod")));
        using GameTree store = Store(GameFolders.Dll("sm86"));

        (await bed.CheckAsync(Stored(bed.Request(RouteId.OptiScaler, _sm86), store), Ct)).AssertSays(BlockerKind.OwnFrameGeneration, "version.dll is already in");
    }

    [TestMethod]
    public async Task The_same_bytes_or_a_file_of_the_previous_install_under_their_name_are_not()
    {
        using RefusalBed same = new(TreeFile.Bytes("version.dll", GameFolders.Dll("sm86")));
        using RefusalBed ours = new(TreeFile.Bytes("version.dll", GameFolders.Dll("placed")));
        Recorded.Plant(ours.Tree.Root, Recorded.Install("", Recorded.Ours("version.dll")));
        using GameTree store = Store(GameFolders.Dll("sm86"));

        (await same.CheckAsync(Stored(same.Request(RouteId.OptiScaler, _sm86), store), Ct)).AssertAllowed();
        (await ours.CheckAsync(Stored(ours.Request(RouteId.OptiScaler, _sm86), store), Ct)).AssertAllowed();
    }

    [TestMethod]
    public async Task The_persons_own_add_on_must_be_there()
    {
        using RefusalBed bed = new();

        (await bed.CheckAsync(bed.Request(RouteId.Native, new RouteSettings { RenodxFile = bed.Tree.PathOf("gone.addon64") }), Ct)).AssertSays(BlockerKind.LocalAddOn, "is not there any more");
    }

    [TestMethod]
    public async Task The_persons_own_add_on_must_be_64_bit()
    {
        using RefusalBed bed = new(TreeFile.Bytes("mine32.addon32", new Executables.TestPe(Executables.TestPe.I386).ToBytes()), TreeFile.Bytes("mine.addon64", GameFolders.Dll()));

        (await bed.CheckAsync(bed.Request(RouteId.Native, new RouteSettings { RenodxFile = bed.Tree.PathOf("mine32.addon32") }), Ct)).AssertSays(BlockerKind.LocalAddOn, "is not a 64-bit add-on");
        (await bed.CheckAsync(bed.Request(RouteId.Native, new RouteSettings { RenodxFile = bed.Tree.PathOf("mine.addon64") }), Ct)).AssertAllowed();
    }

    [TestMethod]
    public async Task A_file_that_is_no_dll_is_no_64_bit_add_on()
    {
        using RefusalBed bed = new(TreeFile.Text("notes.addon64", "not a DLL"));

        (await bed.CheckAsync(bed.Request(RouteId.Native, new RouteSettings { RenodxFile = bed.Tree.PathOf("notes.addon64") }), Ct)).AssertSays(BlockerKind.LocalAddOn, "is not a 64-bit add-on");
    }

    private static RefusalRequest Stored(RefusalRequest request, GameTree store) => request with { OwnFrameGenerationStore = store.Root };

    // The tool's own copy of dlssg_for_sm86's files, its DLL holding these bytes.
    private static GameTree Store(byte[] dll) =>
        GameTree.Of(TreeFile.Bytes(@"sm86\version.dll", dll), TreeFile.Text(@"sm86\dlssg_sm86.ini", "[dlssg]\r\n"));
}
