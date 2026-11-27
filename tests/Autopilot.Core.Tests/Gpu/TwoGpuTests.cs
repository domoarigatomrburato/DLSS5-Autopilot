using Autopilot.Core.Gpu;
using static Autopilot.Core.Tests.Gpu.GpuReadings;

namespace Autopilot.Core.Tests.Gpu;

/// <summary>An NVIDIA card beside an Intel or AMD GPU is the machine Windows' graphics preference is set on, naming the other one.</summary>
[TestClass]
public sealed class TwoGpuTests
{
    [TestMethod]
    [DataRow(Intel, "Intel(R) UHD Graphics 770", GpuVendor.Intel)]
    [DataRow(Amd, "AMD Radeon(TM) Graphics", GpuVendor.Amd)]
    [DataRow(AmdAlt, "AMD Radeon(TM) 780M", GpuVendor.Amd)]
    public void An_nvidia_card_beside_another_gpu_names_it(int vendor, string name, GpuVendor other) =>
        Assert.AreEqual(other, Facts(Adapter(vendor, name), Card("NVIDIA GeForce RTX 4060 Laptop GPU")).OtherGpu);

    [TestMethod]
    public void An_nvidia_card_alone_has_no_other_gpu() =>
        Assert.IsNull(Facts(Card("NVIDIA GeForce RTX 4070")).OtherGpu);

    [TestMethod]
    public void Another_gpu_without_an_nvidia_card_is_no_pair() =>
        Assert.IsNull(Facts(Adapter(Intel, "Intel(R) UHD Graphics"), Adapter(Amd, "AMD Radeon RX 7600")).OtherGpu);

    [TestMethod]
    public void A_software_adapter_is_no_other_gpu() =>
        Assert.IsNull(Facts(new GraphicsAdapter(Intel, "Intel software", Software: true), Card("NVIDIA GeForce RTX 4070")).OtherGpu);

    [TestMethod]
    public void Without_the_adapter_list_the_display_class_names_decide()
    {
        GpuReading reading = new([], null, [new("NVIDIA GeForce RTX 4060 Laptop GPU", null), new("AMD Radeon(TM) Graphics", null)]);

        Assert.AreEqual(GpuVendor.Amd, GpuFacts.From(reading).OtherGpu);
    }

    [TestMethod]
    public void The_display_class_is_not_read_when_the_adapter_list_answers()
    {
        // The display class keeps every card the PC ever had: an Intel GPU taken out years ago is no pair.
        GpuReading reading = new([Card("NVIDIA GeForce RTX 4070")], null, [new("Intel(R) HD Graphics 630", null)]);

        Assert.IsNull(GpuFacts.From(reading).OtherGpu);
    }

    [TestMethod]
    [DataRow("Intel(R) Iris(R) Xe Graphics", GpuVendor.Intel)]
    [DataRow("Advanced Micro Devices Radeon", GpuVendor.Amd)]
    public void Display_class_names_tell_the_vendor(string name, GpuVendor other)
    {
        GpuReading reading = new([], null, [new(name, null), new("GeForce RTX 3060", null)]);

        Assert.AreEqual(other, GpuFacts.From(reading).OtherGpu);
    }
}
