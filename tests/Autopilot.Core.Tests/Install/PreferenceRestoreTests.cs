using Autopilot.Core.Gpu;
using Autopilot.Core.Install;
using static Autopilot.Core.Tests.Install.SharedBeds;

namespace Autopilot.Core.Tests.Install;

/// <summary>
/// The graphics preference taken back out when an install no longer sets it, by the restore rules: only our token, never a
/// value the person changed since; one that cannot be released is released by the next install. Planned only on a PC with
/// two GPUs and the option on.
/// </summary>
[TestClass]
public sealed class PreferenceRestoreTests(TestContext testContext)
{
    private const string High = "GpuPreference=2;";
    private const string Windowed = "SwapEffectUpgradeEnable=1;";

    private CancellationToken Ct => testContext.CancellationToken;

    [TestMethod]
    [DataRow(null, High, null, DisplayName = "untouched, no value before: deleted")]
    [DataRow(Windowed, Windowed + High, Windowed, DisplayName = "untouched: the old string exactly")]
    [DataRow(Windowed, "SwapEffectUpgradeEnable=0;GpuPreference=2;AutoHDREnable=1;", "SwapEffectUpgradeEnable=0;AutoHDREnable=1;", DisplayName = "other tokens changed: only ours removed")]
    [DataRow(null, "GpuPreference=2;AutoHDREnable=1;", "AutoHDREnable=1;", DisplayName = "a token added since: only ours removed")]
    [DataRow(Windowed, Windowed + "GpuPreference=1;", Windowed + "GpuPreference=1;", DisplayName = "our token changed: left alone")]
    [DataRow(Windowed, Windowed, Windowed, DisplayName = "our token gone: left alone")]
    public async Task Turning_the_option_off_takes_the_preference_back_out_by_the_restore_rules(string? before, string now, string? after)
    {
        using TransactionBed bed = NewGame();
        if (before is not null)
        {
            bed.SetPreference(before);
        }

        _ = await bed.CommitAsync(Ct, Preference());
        bed.SetPreference(now);

        _ = await bed.CommitAsync(Ct, Dxgi());

        Assert.AreEqual(after, bed.PreferenceOf());
        Assert.AreEqual("", await bed.PreferencesAsync(Ct));
        Assert.IsNull(await bed.ReleasingAsync(Ct));
    }

    [TestMethod]
    [DataRow(Windowed + High, Windowed, DisplayName = "our token there: the old string back")]
    [DataRow(Windowed + "GpuPreference=1;", Windowed + "GpuPreference=1;", DisplayName = "our token changed: left alone")]
    public async Task An_uninstall_puts_the_graphics_preference_back_while_our_token_is_there(string now, string after)
    {
        using TransactionBed bed = NewGame();
        bed.SetPreference(Windowed);
        _ = await bed.CommitAsync(Ct, Preference());
        bed.SetPreference(now);

        UninstallFinished finished = await bed.UninstallFinishedAsync(Ct);

        Assert.IsTrue(finished.Complete);
        Assert.AreEqual(after, bed.PreferenceOf());
    }

    [TestMethod]
    public async Task A_value_deleted_since_is_not_put_back()
    {
        using TransactionBed bed = NewGame();
        _ = await bed.CommitAsync(Ct, Preference());
        _ = bed.User().Remove(PreferenceKey, bed.Program());

        _ = await bed.CommitAsync(Ct, Dxgi());

        Assert.IsNull(bed.PreferenceOf());
    }

    [TestMethod]
    public async Task A_preference_that_cannot_be_released_stays_on_the_record_and_is_released_by_the_next_install()
    {
        using TransactionBed bed = NewGame();
        _ = await bed.CommitAsync(Ct, Preference());
        _ = bed.User().RefuseWrites(PreferenceKey);

        _ = await bed.CommitAsync(Ct, Dxgi());
        string? left = await bed.ReleasingAsync(Ct);
        bed.User().Allow();
        _ = await bed.CommitAsync(Ct, Dxgi());

        Assert.AreEqual(bed.Edit(null, High), left);
        Assert.IsNull(bed.PreferenceOf());
        Assert.IsNull(await bed.ReleasingAsync(Ct));
    }

    [TestMethod]
    public void Steps_are_planned_only_with_two_gpus_and_the_option_on()
    {
        var hybrid = GpuFacts.From(new GpuReading([new(0x8086, "Intel(R) UHD Graphics", false), new(0x10DE, "NVIDIA GeForce RTX 4060 Laptop GPU", false)], null, []));
        var single = GpuFacts.From(new GpuReading([new(0x10DE, "NVIDIA GeForce RTX 4070", false)], null, []));

        CollectionAssert.AreEqual(new PlanStep[] { new SetGraphicsPreference("Game.exe", GpuVendor.Intel), new SetGraphicsPreference("host64/dlss5-feed-host64.exe", GpuVendor.Intel) }, GraphicsPreferenceSteps.Of(hybrid, optionOn: true, "Game.exe", "host64/dlss5-feed-host64.exe").ToList());
        Assert.IsEmpty(GraphicsPreferenceSteps.Of(hybrid, optionOn: false, "Game.exe"));
        Assert.IsEmpty(GraphicsPreferenceSteps.Of(single, optionOn: true, "Game.exe"));
    }
}
