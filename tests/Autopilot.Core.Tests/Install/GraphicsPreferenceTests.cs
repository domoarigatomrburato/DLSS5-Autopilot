using Autopilot.Core.Install;
using Autopilot.Core.Tests.TestBeds;
using static Autopilot.Core.Tests.Install.SharedBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>
/// Windows' graphics preference set to "High performance" for the game on a PC with two GPUs: only our token added, a
/// choice the person made kept, and never a reason for an install to stop.
/// </summary>
[TestClass]
public sealed class GraphicsPreferenceTests(TestContext testContext)
{
    private const string High = "GpuPreference=2;";
    private const string Windowed = "SwapEffectUpgradeEnable=1;";
    private const string Helper = "host64/dlss5-feed-host64.exe";

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    public async Task The_game_is_set_to_high_performance_and_the_record_says_what_was_there()
    {
        using TransactionBed bed = NewGame();

        _ = await bed.CommitAsync(Ct, Preference());

        Assert.AreEqual(High, bed.PreferenceOf());
        Assert.AreEqual(bed.Edit(null, High), await bed.PreferencesAsync(Ct));
    }

    [TestMethod]
    public async Task Other_tokens_in_the_value_are_kept_as_found()
    {
        using TransactionBed bed = NewGame();
        bed.SetPreference("SwapEffectUpgradeEnable=1;AutoHDREnable=2097;");

        _ = await bed.CommitAsync(Ct, Preference());

        Assert.AreEqual("SwapEffectUpgradeEnable=1;AutoHDREnable=2097;GpuPreference=2;", bed.PreferenceOf());
    }

    [TestMethod]
    [DataRow("GpuPreference=1;", true)]
    [DataRow("GpuPreference=0;", true)]
    [DataRow("GpuPreference=2;", false)]
    public async Task A_choice_the_person_made_is_left_alone_and_never_recorded(string theirs, bool warnsOfTheOtherGpu)
    {
        using TransactionBed bed = NewGame();
        bed.SetPreference(Windowed + theirs);

        InstallCommitted committed = await bed.CommitAsync(Ct, Preference());

        Assert.AreEqual(Windowed + theirs, bed.PreferenceOf());
        Assert.AreEqual("", await bed.PreferencesAsync(Ct));
        string note = committed.Said("Game.exe already had a Windows graphics setting")!;
        Assert.AreEqual(warnsOfTheOtherGpu, note.Contains("the Intel GPU", StringComparison.Ordinal), note);
    }

    [TestMethod]
    public async Task A_value_named_by_another_spelling_of_the_path_is_the_programs()
    {
        using TransactionBed bed = NewGame();
        string link = bed.Tree.Root + "-link";
        _ = Directory.CreateSymbolicLink(link, bed.Tree.Root);
        _ = bed.User().Set(PreferenceKey, Path.Join(link, "Game.exe"), "GpuPreference=1;");

        _ = await bed.CommitAsync(Ct, Preference());

        Assert.IsNull(bed.PreferenceOf(), "nothing is added under a second name");
        Assert.AreEqual("", await bed.PreferencesAsync(Ct));
    }

    [TestMethod]
    public async Task A_reinstall_finding_our_own_value_keeps_its_first_record()
    {
        using TransactionBed bed = NewGame();
        bed.SetPreference(Windowed);
        _ = await bed.CommitAsync(Ct, Preference());

        _ = await bed.CommitAsync(Ct, Preference());

        Assert.AreEqual(bed.Edit(Windowed, Windowed + High), await bed.PreferencesAsync(Ct));
        Assert.AreEqual(Windowed + High, bed.PreferenceOf());
    }

    [TestMethod]
    public async Task A_key_that_refuses_writes_is_a_warning_and_the_install_commits()
    {
        using TransactionBed bed = New(TreeFile.Bytes("Game.exe", Dll64), TreeFile.Bytes(Helper, Dll64));
        _ = bed.User().RefuseWrites(PreferenceKey, bed.Program(Helper));

        InstallCommitted committed = await bed.Commit32Async(Ct, Preference(), Preference(Helper), Dxgi());

        Assert.IsNotNull(committed.Said("dlss5-feed-host64.exe", "Settings > System > Display > Graphics"));
        Assert.AreEqual(bed.Edit(null, High), await bed.PreferencesAsync(Ct), "the value written so far stays recorded");
        Assert.IsTrue(bed.Holds("dxgi.dll", Dll32));
    }

    [TestMethod]
    public async Task A_key_that_cannot_be_read_is_a_warning_and_the_earlier_record_is_carried_forward()
    {
        using TransactionBed bed = NewGame();
        _ = await bed.CommitAsync(Ct, Preference());
        _ = bed.User().RefuseReads(PreferenceKey);

        InstallCommitted committed = await bed.CommitAsync(Ct, Preference());

        Assert.IsNotNull(committed.Said("Could not set the Windows graphics setting"));
        Assert.AreEqual(bed.Edit(null, High), await bed.PreferencesAsync(Ct));
    }

    [TestMethod]
    public async Task One_program_named_twice_is_a_plan_conflict()
    {
        using TransactionBed bed = NewGame();

        Assert.AreEqual(InstallRefusal.PlanConflict, await bed.RefusalAsync(Ct, Preference(), Preference("game.EXE")));
    }

    [TestMethod]
    public async Task A_program_outside_the_game_folder_is_refused()
    {
        using TransactionBed bed = NewGame();

        Assert.AreEqual(InstallRefusal.PathOutsideGameFolder, await bed.RefusalAsync(Ct, Preference(@"..\Other\Game.exe")));
    }

    [TestMethod]
    public async Task A_plan_with_a_graphics_preference_and_no_registry_is_refused()
    {
        using TransactionBed bed = new(TreeFile.Bytes("Game.exe", Dll64));

        Assert.AreEqual(InstallRefusal.SharedPlacesMissing, await bed.RefusalAsync(Ct, Preference()));
    }
}
