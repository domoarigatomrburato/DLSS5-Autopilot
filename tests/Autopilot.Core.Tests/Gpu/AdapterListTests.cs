using Autopilot.Core.Gpu;
using static Autopilot.Core.Tests.Gpu.GpuReadings;

namespace Autopilot.Core.Tests.Gpu;

/// <summary>Of everything Windows lists, the NVIDIA card decides; without one, the vendor found says why nothing runs.</summary>
[TestClass]
public sealed class AdapterListTests
{
    private const string Rtx4070 = "NVIDIA GeForce RTX 4070";

    [TestMethod]
    public void An_nvidia_card_alone_is_the_card()
    {
        GpuFacts facts = Facts(Card(Rtx4070));

        Assert.AreEqual(Rtx4070, facts.Card?.Name);
        Assert.AreEqual(GpuVendor.Nvidia, facts.Vendor);
        Assert.IsNull(facts.Refusal);
    }

    [TestMethod]
    [DataRow(Intel, "Intel(R) UHD Graphics 770")]
    [DataRow(Amd, "AMD Radeon(TM) Graphics")]
    [DataRow(AmdAlt, "AMD Radeon(TM) 780M")]
    public void An_integrated_gpu_beside_the_nvidia_card_changes_nothing(int vendor, string name)
    {
        GpuFacts facts = Facts(Adapter(vendor, name), Card("NVIDIA GeForce RTX 4060 Laptop GPU"));

        Assert.AreEqual(new GpuCard("NVIDIA GeForce RTX 4060 Laptop GPU", 89, HasTensorCores: true), facts.Card);
        Assert.AreEqual(GpuVendor.Nvidia, facts.Vendor);
        Assert.IsNull(facts.Refusal);
    }

    [TestMethod]
    [DataRow(Amd)]
    [DataRow(AmdAlt)]
    public void An_amd_card_alone_is_refused_with_the_amd_answer(int vendor)
    {
        GpuFacts facts = Facts(Adapter(vendor, "AMD Radeon RX 7600"));

        Assert.IsNull(facts.Card);
        Assert.AreEqual(GpuVendor.Amd, facts.Vendor);
        Assert.AreEqual(new NoNvidiaCard(GpuVendor.Amd), facts.Refusal);
        string explanation = facts.Refusal!.Explain();
        StringAssert.StartsWith(explanation, "No NVIDIA graphics card found", StringComparison.Ordinal);
        StringAssert.Contains(explanation, "nvngx_dlssnr.dll", StringComparison.Ordinal);
        StringAssert.Contains(explanation, "HIP", StringComparison.Ordinal);
        StringAssert.Contains(explanation, "Discord", StringComparison.Ordinal);
    }

    [TestMethod]
    public void An_intel_card_alone_is_refused_with_the_generic_answer()
    {
        GpuFacts facts = Facts(Adapter(Intel, "Intel(R) Arc(TM) A770 Graphics"));

        Assert.AreEqual(GpuVendor.Intel, facts.Vendor);
        Assert.AreEqual(new NoNvidiaCard(GpuVendor.Intel), facts.Refusal);
        string explanation = facts.Refusal!.Explain();
        StringAssert.StartsWith(explanation, "No NVIDIA graphics card found", StringComparison.Ordinal);
        StringAssert.Contains(explanation, "Intel", StringComparison.Ordinal);
        Assert.DoesNotContain("HIP", explanation);
    }

    [TestMethod]
    public void Amd_beside_intel_gets_the_amd_answer() =>
        Assert.AreEqual(new NoNvidiaCard(GpuVendor.Amd), Facts(Adapter(Intel, "Intel(R) UHD Graphics"), Adapter(Amd, "AMD Radeon RX 7600")).Refusal);

    [TestMethod]
    public void A_software_adapter_never_counts()
    {
        GpuFacts facts = Facts(new GraphicsAdapter(MicrosoftVendor, "Microsoft Basic Render Driver", Software: true), Card(Rtx4070));

        Assert.AreEqual(Rtx4070, facts.Card?.Name);
        Assert.IsNull(facts.Refusal);
    }

    [TestMethod]
    public void A_software_nvidia_adapter_is_not_the_card() =>
        Assert.IsNull(Facts(new GraphicsAdapter(Nvidia, Rtx4070, Software: true)).Card);

    [TestMethod]
    public void A_software_adapter_beside_an_amd_card_is_still_refused() =>
        Assert.AreEqual(
            new NoNvidiaCard(GpuVendor.Amd),
            Facts(new GraphicsAdapter(MicrosoftVendor, "Microsoft Basic Display Adapter", Software: true), Adapter(Amd, "AMD Radeon RX 7600")).Refusal);

    [TestMethod]
    public void Nothing_readable_refuses_nothing()
    {
        GpuFacts facts = Facts();

        Assert.IsNull(facts.Card);
        Assert.AreEqual(GpuVendor.None, facts.Vendor);
        Assert.IsNull(facts.Refusal);
    }

    [TestMethod]
    public void A_basic_display_adapter_alone_refuses_nothing()
    {
        // Windows shows it when a card has no driver yet, so it says nothing about which card is there.
        GpuFacts facts = Facts(Adapter(MicrosoftVendor, "Microsoft Basic Display Adapter"));

        Assert.AreEqual(GpuVendor.None, facts.Vendor);
        Assert.IsNull(facts.Refusal);
    }

    [TestMethod]
    public void An_unreadable_nvidia_name_beside_a_known_one_refuses_nothing()
    {
        GpuFacts facts = Facts(Card("NVIDIA GeForce GTX 1660"), Card("NVIDIA GeForce RTX 7090"));

        Assert.AreEqual("NVIDIA GeForce GTX 1660", facts.Card?.Name);
        Assert.IsNull(facts.Refusal);
    }

    [TestMethod]
    public void The_newest_architecture_wins_in_any_order()
    {
        Assert.AreEqual(Rtx4070, Facts(Card("NVIDIA GeForce RTX 3060"), Card(Rtx4070)).Card?.Name);
        Assert.AreEqual(Rtx4070, Facts(Card(Rtx4070), Card("NVIDIA GeForce RTX 3060")).Card?.Name);
    }

    [TestMethod]
    public void A_newer_card_beside_an_old_one_is_not_refused()
    {
        GpuFacts facts = Facts(Card("NVIDIA GeForce GTX 1080 Ti"), Card(Rtx4070));

        Assert.AreEqual(Rtx4070, facts.Card?.Name);
        Assert.IsNull(facts.Refusal);
    }

    [TestMethod]
    public void Of_two_turing_cards_the_one_with_tensor_cores_wins()
    {
        GpuFacts first = Facts(Card("NVIDIA GeForce GTX 1660 Ti"), Card("NVIDIA GeForce RTX 2060"));
        GpuFacts second = Facts(Card("NVIDIA GeForce RTX 2060"), Card("NVIDIA GeForce GTX 1660 Ti"));

        Assert.AreEqual("NVIDIA GeForce RTX 2060", first.Card?.Name);
        Assert.AreEqual("NVIDIA GeForce RTX 2060", second.Card?.Name);
        Assert.IsNull(first.Refusal);
    }

    [TestMethod]
    [DataRow(Intel, "Intel(R) UHD Graphics 770", true, DisplayName = "NVIDIA and Intel")]
    [DataRow(Amd, "AMD Radeon(TM) Graphics", true, DisplayName = "NVIDIA and AMD")]
    [DataRow(Nvidia, "NVIDIA GeForce RTX 3060", false, DisplayName = "two NVIDIA cards")]
    public void Two_gpus_are_an_nvidia_card_and_an_intel_or_amd_one(int vendor, string name, bool twoGpus) =>
        Assert.AreEqual(twoGpus, Facts(Card(Rtx4070), Adapter(vendor, name)).TwoGpus);

    [TestMethod]
    public void An_nvidia_card_alone_or_beside_a_software_adapter_is_not_two_gpus()
    {
        Assert.IsFalse(Facts(Card(Rtx4070)).TwoGpus);
        Assert.IsFalse(Facts(Card(Rtx4070), new GraphicsAdapter(MicrosoftVendor, "Microsoft Basic Render Driver", Software: true)).TwoGpus);
    }
}
