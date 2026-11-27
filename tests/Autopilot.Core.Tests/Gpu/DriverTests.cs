using Autopilot.Core.Gpu;
using static Autopilot.Core.Tests.Gpu.GpuReadings;

namespace Autopilot.Core.Tests.Gpu;

/// <summary>The driver's version as people know it, where it is read from, and how it compares.</summary>
[TestClass]
public sealed class DriverTests
{
    [TestMethod]
    [DataRow("32.0.15.6094", "560.94")]
    [DataRow("32.0.16.1664", "616.64")]
    [DataRow("32.0.16.1692", "616.92")]
    [DataRow("32.0.15.5612", "556.12")]
    [DataRow("31.0.15.8180", "581.80")]
    public void The_file_version_becomes_the_version_people_know(string fileVersion, string expected) =>
        Assert.AreEqual(expected, Driver(fileVersion).Driver);

    [TestMethod]
    [DataRow("not a version")]
    [DataRow("616.92")]
    [DataRow("32.0.16")]
    [DataRow("32.0.16.16x2")]
    [DataRow("32.0.1.12")]
    public void A_file_version_not_in_four_dotted_parts_comes_back_as_given(string fileVersion) =>
        Assert.AreEqual(fileVersion, Driver(fileVersion).Driver);

    [TestMethod]
    public void Without_nvapi_the_newest_nvidia_entry_of_the_registry_wins_over_a_stale_one()
    {
        GpuFacts facts = Driver(
            null,
            new DisplayClassEntry("NVIDIA GeForce RTX 3060", "31.0.15.8180"),
            new DisplayClassEntry("Intel(R) UHD Graphics 770", "32.0.17.9999"),
            new DisplayClassEntry("NVIDIA GeForce RTX 4070", "32.0.16.1692"),
            new DisplayClassEntry("NVIDIA GeForce RTX 2060", null),
            new DisplayClassEntry("NVIDIA GeForce GTX 1060", "garbage"));

        Assert.AreEqual("616.92", facts.Driver);
    }

    [TestMethod]
    public void Nvapi_wins_over_the_registry() =>
        Assert.AreEqual("616.64", Driver("32.0.16.1664", new DisplayClassEntry("NVIDIA GeForce RTX 4070", "32.0.16.1692")).Driver);

    [TestMethod]
    public void No_nvapi_and_no_nvidia_entry_is_no_driver()
    {
        Assert.IsNull(Driver(null).Driver);
        Assert.IsNull(Driver(null, new DisplayClassEntry("AMD Radeon RX 7600", "32.0.21001.9024")).Driver);
        Assert.IsNull(Driver(null, new DisplayClassEntry("NVIDIA GeForce RTX 4070", "garbage")).Driver);
    }

    [TestMethod]
    [DataRow("616.92", "616.56", true)]
    [DataRow("616.56", "616.56", true)]
    [DataRow("616.55", "616.56", false)]
    [DataRow("581.80", "616.56", false)]
    [DataRow("1000.01", "616.56", true)]
    [DataRow("616", "616.0", false)]
    [DataRow("616.0", "616", true)]
    public void A_readable_driver_compares_as_dotted_integers(string driver, string version, bool expected) =>
        Assert.AreEqual(expected, NvidiaDriver.AtLeast(driver, version));

    [TestMethod]
    [DataRow(null, "616.56")]
    [DataRow("", "616.56")]
    [DataRow("not a version", "616.56")]
    [DataRow("616..92", "616.56")]
    [DataRow("-616.92", "616.56")]
    [DataRow("616.92", "")]
    [DataRow("616.92", "latest")]
    public void An_unreadable_side_answers_unknown(string? driver, string version) =>
        Assert.IsNull(NvidiaDriver.AtLeast(driver, version));

    [TestMethod]
    public void The_thresholds_split_the_drivers_in_the_reports()
    {
        Assert.IsFalse(NvidiaDriver.AtLeast("610.88", NvidiaDriver.FirstWithNeuralRuntime));
        Assert.IsTrue(NvidiaDriver.AtLeast("616.56", NvidiaDriver.FirstWithNeuralRuntime));
        Assert.IsFalse(NvidiaDriver.AtLeast("616.56", NvidiaDriver.FirstWithRenodxFault));
        Assert.IsTrue(NvidiaDriver.AtLeast("616.64", NvidiaDriver.FirstWithRenodxFault));
        Assert.IsTrue(NvidiaDriver.AtLeast("616.86", NvidiaDriver.FirstWithRenodxFault));
    }
}
