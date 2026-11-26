namespace Autopilot.Core.Tests.Library;

public abstract class LibraryTestBase(TestContext testContext)
{
    protected CancellationToken Ct => testContext.CancellationToken;

    /// <summary>The candidates must be exactly these, in this order; the failure shows what came back.</summary>
    protected static void AssertOrder(string[] actual, params string[] expected) =>
        CollectionAssert.AreEqual(expected, actual, $"got: {string.Join(", ", actual)}");
}
