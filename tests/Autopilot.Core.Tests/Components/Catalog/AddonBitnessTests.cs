using Autopilot.Core.Components;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Tests.Archives;

namespace Autopilot.Core.Tests.Components.Catalog;

/// <summary>An <c>.addon64</c> must be a 64-bit Windows binary and an <c>.addon32</c> a 32-bit one before it is placed.</summary>
[TestClass]
public sealed class AddonBitnessTests(TestContext testContext)
{
    [TestMethod]
    [DataRow("gitc-uplift.addon64", 32, 64)]
    [DataRow("gitc-uplift.addon32", 64, 32)]
    [DataRow("gitc-uplift.addon64", 0, 64)]
    public async Task An_add_on_that_is_not_a_windows_binary_of_its_bitness_is_refused(string name, int bits, int wanted)
    {
        using ComponentsBed bed = new(testContext);
        ComponentRelease release = FetchFixtures.Served(bed, ComponentIds.Uplift, "GITC-Uplift-1.0.0.zip", ZipOf(name, bits), name);

        ComponentException failure = await Assert.ThrowsAsync<ComponentException>(() => bed.Files.FetchAsync(release, null, bed.Cancellation));

        Assert.AreEqual(ComponentFailure.WrongBitness, failure.Reason);
        Assert.AreEqual($"{name} from ghostinthecamera/GITC-Uplift v1.0.0 is not a {wanted}-bit Windows add-on. Nothing was placed.", failure.Message);
    }

    // A zip holding the add-on under Addons/: a 32-bit or 64-bit binary, or a file that only starts like one (0).
    private static byte[] ZipOf(string name, int bits) =>
        FetchFixtures.Archive(
            ArchiveFormat.Zip,
            new TestMember(
                "Addons/" + name,
                bits switch
                {
                    32 => FetchFixtures.Addon32,
                    64 => FetchFixtures.Addon64,
                    _ => FetchFixtures.NotAnAddon,
                }));
}
