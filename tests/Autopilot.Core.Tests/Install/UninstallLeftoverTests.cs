using Autopilot.Core.Install;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>What uninstall does last: the game's state dropped and the record deleted only when nothing is left, and the changes outside the files kept on the record until they are undone.</summary>
[TestClass]
public sealed class UninstallLeftoverTests(TestContext testContext)
{
    private static readonly LayerRegistration _registration = new(RegisteredLayer.VulkanLayer64, Reused: false, @"C:\Users\a\AppData\Local\invidia-claritatis\reshade-vulkan\ReShade64.json");

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task The_games_state_is_dropped_once_nothing_is_left()
    {
        using TransactionBed bed = await UninstallBed.OursAsync(Ct, "a.dll");
        bed.SetAttempts();

        _ = await bed.UninstallFinishedAsync(Ct);

        Assert.AreEqual(0, bed.Attempts());
    }

    [TestMethod]
    public async Task The_games_state_stays_while_something_is_left()
    {
        using TransactionBed bed = await UninstallBed.OursAsync(Ct, "a.dll");
        bed.SetAttempts();
        bed.Seam = new HeldPaths().Hold(bed.Tree.PathOf("a.dll")).Seam;

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.AreEqual(1, finished.Left);
        Assert.AreEqual(3, bed.Attempts());
    }

    [TestMethod]
    public async Task Outside_changes_not_undone_yet_stay_on_the_record_and_the_message_says_so()
    {
        using TransactionBed bed = await WithOutsideChangesAsync(Ct);
        bed.SetAttempts();

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.IsTrue(await bed.RecordAsync(Ct) is { State: RecordState.Removing, Entries.Count: 0, Registrations.Count: 1, GraphicsPreferences.Count: 1, ConfigEdits.Count: 1 });
        Assert.IsTrue(finished is { OutsideKept: true, Complete: false, Left: 0 } && !bed.Exists("a.dll"));
        Assert.EndsWith("the install record keeps them.", finished.Message, StringComparison.Ordinal);
        Assert.AreEqual(3, bed.Attempts());
    }

    [TestMethod]
    public async Task Outside_changes_undone_let_the_record_go()
    {
        using TransactionBed bed = await WithOutsideChangesAsync(Ct);
        UndoAll outside = new();

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct, outside);

        Assert.IsTrue(finished.Complete);
        Assert.IsNull(await bed.FindAsync(Ct));
        Assert.AreEqual(3, outside.Undone);
    }

    [TestMethod]
    public async Task A_rollback_never_deletes_a_record_still_naming_outside_changes()
    {
        using TransactionBed bed = OpenWithRegistration();

        _ = await bed.RecoverAsync(Ct);

        Assert.IsTrue(await bed.RecordAsync(Ct) is { State: RecordState.Removing, Registrations.Count: 1 });
    }

    [TestMethod]
    public async Task A_rollback_lets_the_record_go_once_its_outside_changes_are_undone()
    {
        using TransactionBed bed = OpenWithRegistration();
        UndoAll outside = new();

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct, outside);

        Assert.IsTrue(finished is { Recovered: true, Complete: true });
        Assert.IsNull(await bed.FindAsync(Ct));
        Assert.AreEqual(1, outside.Undone);
    }

    // An install left open, registering a layer.
    private static TransactionBed OpenWithRegistration()
    {
        TransactionBed bed = new(GameFolders.Empty);
        Recorded.Plant(bed.Tree.Root, Recorded.Install() with { State = RecordState.Installing, Registrations = [_registration] });
        return bed;
    }

    private static async Task<TransactionBed> WithOutsideChangesAsync(CancellationToken cancellationToken)
    {
        TransactionBed bed = await UninstallBed.OursAsync(cancellationToken, "a.dll");
        Recorded.Plant(bed.Tree.Root, WithOutside(await bed.RecordAsync(cancellationToken)));
        return bed;
    }

    private static InstallRecord WithOutside(InstallRecord record) => record with
    {
        Registrations = [_registration],
        GraphicsPreferences = [new GraphicsPreferenceEdit(@"C:\Games\Game.exe", null, "GpuPreference=2;")],
        ConfigEdits = [new ConfigEdit("rtx.conf", null, "rtx.neuralRendering.enable", null, false, true, "True", true)],
    };

    /// <summary>Outside changes as a later sub-task undoes them: every one.</summary>
    private sealed class UndoAll : IOutsideChanges
    {
        public int Undone { get; private set; }

        public Task<IReadOnlyList<ConfigEdit>> UndoConfigEditsAsync(IReadOnlyList<ConfigEdit> edits, InstallPaths paths, CancellationToken cancellationToken) => None(edits);

        public Task<IReadOnlyList<GraphicsPreferenceEdit>> UndoGraphicsPreferencesAsync(IReadOnlyList<GraphicsPreferenceEdit> preferences, CancellationToken cancellationToken) => None(preferences);

        public Task<IReadOnlyList<LayerRegistration>> UndoRegistrationsAsync(IReadOnlyList<LayerRegistration> registrations, CancellationToken cancellationToken) => None(registrations);

        private Task<IReadOnlyList<T>> None<T>(IReadOnlyList<T> undone)
        {
            Undone += undone.Count;
            return Task.FromResult<IReadOnlyList<T>>([]);
        }
    }
}
