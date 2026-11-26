namespace Autopilot.Core.Tests.Library.Protection;

public abstract class ProtectionTestBase(TestContext testContext) : LibraryTestBase(testContext)
{
    /// <summary>The list must be exactly these names, in this order; the failure shows what came back.</summary>
    protected static void AssertNames(IReadOnlyList<string> actual, params string[] expected) =>
        CollectionAssert.AreEqual(expected, actual.ToArray(), $"got: {string.Join(", ", actual)}");
}
