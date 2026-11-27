using Autopilot.Core.Install;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>The preview is the install it describes, and shows it without writing anything.</summary>
[TestClass]
public sealed class PreviewFidelityTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task The_preview_is_what_the_transaction_then_does_to_the_folder()
    {
        using RefusalBed bed = new([.. PreviewScenes.Folder(), TreeFile.Bytes("old.addon64", PreviewScenes.Ours)]);

        (InstallPreview preview, FolderChange change) = await bed.PreviewThenInstallAsync(Ct);

        CollectionAssert.AreEquivalent(change.Written.ToArray(), preview.Written.Select(file => file.Path).ToArray());
        CollectionAssert.AreEquivalent(change.Removed.ToArray(), preview.CleanedUp.Except(preview.Written.Select(file => file.Path)).ToArray());
        Assert.IsTrue(change.BackedUpIntact, string.Join(", ", preview.BackedUp));
        CollectionAssert.Contains(preview.CleanedUp.ToArray(), "old.addon64");
    }

    [TestMethod]
    public async Task The_preview_writes_nothing_in_the_folder_or_the_data_folder()
    {
        using RefusalBed bed = new(PreviewScenes.Folder());
        TreeSnapshot folder = bed.Bed.Snapshot();
        var data = TreeSnapshot.Of(bed.Bed.DataFolder);

        _ = await bed.PreviewAsync(PreviewScenes.BetaRequest(bed), Ct);

        folder.AssertSame(bed.Bed.Snapshot());
        data.AssertSame(TreeSnapshot.Of(bed.Bed.DataFolder));
    }
}
