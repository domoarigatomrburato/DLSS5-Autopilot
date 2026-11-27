using Autopilot.Core.Install;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>What the preview says is cleaned up first, per install.md's preview: the previous install's files, and nothing a pattern stands for.</summary>
[TestClass]
public sealed class PreviewCleanUpTests(TestContext testContext)
{
    private const string OldShader = "reshade-shaders/Shaders/lumenite_old.fxh";

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_pattern_stands_for_the_previous_installs_files_it_names()
    {
        using RefusalBed bed = PreviousShaderAndAddOn();

        InstallPreview preview = await bed.PreviewAsync(bed.Request(PreviewScenes.PatternPlan(bed)), Ct);

        Assert.AreEqual("old.addon64", string.Join(", ", preview.CleanedUp), "the previous lumenite file is the download's to replace");
    }

    [TestMethod]
    public async Task The_games_original_put_back_from_its_backup_is_cleaned_up()
    {
        using RefusalBed bed = SwappedDxgi();

        InstallPreview preview = await bed.PreviewAsync(bed.Request(RouteId.Native, placed: "renodx-dlss5.addon64"), Ct);

        Assert.Contains("dxgi.dll", preview.CleanedUp);
    }

    // A previous install's lumenite shader and add-on, both recorded.
    private static RefusalBed PreviousShaderAndAddOn()
    {
        RefusalBed bed = new(TreeFile.Bytes(OldShader, PreviewScenes.Ours), TreeFile.Bytes("old.addon64", PreviewScenes.Ours));
        Recorded.Plant(bed.Tree.Root, Recorded.Install("", Recorded.Ours(OldShader, Plans.Facts(PreviewScenes.Ours)), Recorded.Ours("old.addon64", Plans.Facts(PreviewScenes.Ours))));
        return bed;
    }

    // A previous install's dxgi.dll over the game's, which it kept under its backup name.
    private static RefusalBed SwappedDxgi()
    {
        byte[] games = GameFolders.Dll("the game's");
        RefusalBed bed = new(TreeFile.Bytes("dxgi.dll", PreviewScenes.Ours), TreeFile.Bytes("dxgi.dll" + OriginalFile.SuffixOf(BackupKind.Backup), games));
        Recorded.Plant(bed.Tree.Root, Recorded.Install("", Recorded.Swapped("dxgi.dll", Plans.Facts(games), Plans.Facts(PreviewScenes.Ours))));
        return bed;
    }
}
