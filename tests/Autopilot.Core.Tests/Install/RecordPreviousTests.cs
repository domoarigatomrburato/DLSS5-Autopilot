using Autopilot.Core.Install;

namespace Autopilot.Core.Tests.Install;

/// <summary>An open install's record carries the previous one whole, and names its held files beside the files written at the same paths.</summary>
[TestClass]
public sealed class RecordPreviousTests(TestContext testContext)
{
    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task A_previous_record_carrying_its_own_previous_is_the_wrong_shape()
    {
        using RecordBed bed = new();
        bed.PlantEdited(Recorded.Install() with { Previous = Recorded.Install() with { Previous = Recorded.Install() } }, _ => { });

        await bed.AssertUnreadableAsync(RecordProblem.WrongShape, Ct);
    }

    [TestMethod]
    public async Task A_previous_record_of_a_game_folder_above_this_ones_is_the_wrong_shape()
    {
        using RecordBed bed = new();
        bed.PlantEdited(Recorded.Install() with { Previous = Recorded.Install("Sub") }, _ => { });

        await bed.AssertUnreadableAsync(RecordProblem.WrongShape, Ct);
    }

    [TestMethod]
    public async Task A_held_file_an_original_put_back_and_the_file_written_at_their_path_are_all_kept()
    {
        using RecordBed bed = new();
        RecordEntry held = new("dxgi.dll", EntryClass.Component, EntryState.Held, Recorded.Unhashed, null);
        RecordEntry restored = new("dxgi.dll", EntryClass.Component, EntryState.Restored, null, null);
        await bed.SaveAsync(Recorded.Install("", Recorded.Ours("dxgi.dll"), held, restored) with { State = RecordState.Installing }, Ct);

        Assert.AreEqual("dxgi.dll|dxgi.dll|dxgi.dll", await bed.EntryPathsAsync("", Ct));
    }

    [TestMethod]
    public async Task A_superseded_name_numbered_below_one_is_the_wrong_shape()
    {
        using RecordBed bed = new();
        bed.PlantEdited(Recorded.Install("", Recorded.Ours("dxgi.dll") with { Superseded = 0 }), _ => { });

        await bed.AssertUnreadableAsync(RecordProblem.WrongShape, Ct);
    }
}
