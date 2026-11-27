using Autopilot.Core.Gpu;
using static Autopilot.Core.Tests.Gpu.GpuReadings;

namespace Autopilot.Core.Tests.Gpu;

/// <summary>The card's name says its architecture and whether DLSS can run on it.</summary>
[TestClass]
public sealed class CardTests
{
    public static IEnumerable<object[]> ReportedCards() =>
        File.ReadLines(Path.Join(AppContext.BaseDirectory, "Gpu", "report-cards.txt"))
            .Where(line => !line.StartsWith('#', StringComparison.Ordinal))
            .Select(line => line.Split('|'))
            .Select(fields => new object[] { fields[0], int.Parse(fields[1], System.Globalization.CultureInfo.InvariantCulture) });

    [TestMethod]
    public void The_report_corpus_is_read() => Assert.HasCount(33, ReportedCards().ToList());

    [TestMethod]
    [DynamicData(nameof(ReportedCards))]
    public void Every_card_in_the_reports_gets_the_architecture_its_report_printed(string name, int sm)
    {
        GpuFacts facts = Facts(Card(name));

        Assert.AreEqual(new GpuCard(name, sm, HasTensorCores: true), facts.Card);
        Assert.IsNotNull(facts.Card!.Family);
        Assert.IsNull(facts.Refusal);
    }

    [TestMethod]
    [DataRow("NVIDIA RTX 2000 Ada Generation", 89, "RTX 40 (Ada Lovelace)")]
    [DataRow("NVIDIA RTX 5000 Ada Generation", 89, "RTX 40 (Ada Lovelace)")]
    [DataRow("NVIDIA RTX PRO 6000 Blackwell Workstation Edition", 120, "RTX 50 (Blackwell)")]
    [DataRow("NVIDIA RTX A4000", 86, "RTX 30 (Ampere)")]
    [DataRow("Quadro RTX 4000", 75, "RTX 20 / GTX 16 (Turing)")]
    [DataRow("TITAN RTX", 75, "RTX 20 / GTX 16 (Turing)")]
    [DataRow("NVIDIA GeForce RTX 2060", 75, "RTX 20 / GTX 16 (Turing)")]
    public void A_workstation_or_rtx_card_runs_on_its_architecture(string name, int sm, string family)
    {
        GpuFacts facts = Facts(Card(name));

        Assert.AreEqual(new GpuCard(name, sm, HasTensorCores: true), facts.Card);
        Assert.AreEqual(family, facts.Card!.Family);
        Assert.IsNull(facts.Refusal);
    }

    [TestMethod]
    [DataRow("NVIDIA GeForce GTX 1650")]
    [DataRow("GTX 1660 SUPER")]
    [DataRow("GeForce MX450")]
    [DataRow("NVIDIA GeForce MX550")]
    [DataRow("NVIDIA T1000")]
    [DataRow("NVIDIA T400 4GB")]
    [DataRow("Quadro T2000")]
    public void A_turing_card_without_tensor_cores_keeps_sm_75_and_is_refused(string name)
    {
        GpuFacts facts = Facts(Card(name));

        Assert.AreEqual(new GpuCard(name, 75, HasTensorCores: false), facts.Card);
        Assert.AreEqual(new NoTensorCores(name), facts.Refusal);
        string explanation = facts.Refusal!.Explain();
        StringAssert.StartsWith(explanation, name, StringComparison.Ordinal);
        StringAssert.Contains(explanation, "no tensor cores", StringComparison.Ordinal);
        StringAssert.Contains(explanation, "RTX 20 or newer", StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("NVIDIA GeForce GTX 1080 Ti", 61)]
    [DataRow("NVIDIA GeForce GTX 1060 6GB", 61)]
    [DataRow("NVIDIA GeForce GTX 980", null)]
    [DataRow("NVIDIA GeForce GTX 750 Ti", null)]
    [DataRow("NVIDIA GeForce GT 1030", null)]
    [DataRow("NVIDIA GeForce GT 730", null)]
    public void A_card_older_than_turing_is_refused(string name, int? sm)
    {
        GpuFacts facts = Facts(Card(name));

        Assert.AreEqual(new GpuCard(name, sm, HasTensorCores: false), facts.Card);
        Assert.AreEqual(new OlderThanRtx20(name), facts.Refusal);
        string explanation = facts.Refusal!.Explain();
        StringAssert.StartsWith(explanation, name, StringComparison.Ordinal);
        StringAssert.Contains(explanation, "RTX 20 or newer", StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("NVIDIA GeForce RTX 6090")]
    [DataRow("NVIDIA Graphics Device")]
    public void An_nvidia_name_the_table_cannot_read_is_never_refused(string name)
    {
        GpuFacts facts = Facts(Card(name));

        Assert.AreEqual(new GpuCard(name, null, null), facts.Card);
        Assert.IsNull(facts.Card!.Family);
        Assert.AreEqual(GpuVendor.Nvidia, facts.Vendor);
        Assert.IsNull(facts.Refusal);
    }

    [TestMethod]
    public void A_pascal_card_has_an_architecture_but_no_family() =>
        Assert.IsNull(Facts(Card("NVIDIA GeForce GTX 1080 Ti")).Card!.Family);
}
