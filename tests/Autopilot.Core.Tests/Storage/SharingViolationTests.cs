using Autopilot.Core.Storage;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Storage;

/// <summary>A file another program holds is recognised as such however the error was wrapped, and nothing else is.</summary>
[TestClass]
public sealed class SharingViolationTests
{
    [TestMethod]
    public void A_file_another_handle_holds_is_recognised_bare_and_wrapped()
    {
        using GameTree data = new GameTree("data").Add(TreeFile.Text("a.json", "{}"));
        using FileStream antivirus = new(data.PathOf("a.json"), FileMode.Open, FileAccess.Read, FileShare.None);
        IOException held = Assert.Throws<IOException>(() => new FileStream(data.PathOf("a.json"), FileMode.Open, FileAccess.Read, FileShare.None).Dispose());

        Assert.IsTrue(IoErrors.IsSharingViolation(held));
        Assert.IsTrue(IoErrors.IsSharingViolation(new InvalidOperationException("could not replace", held)));
        Assert.IsTrue(IoErrors.IsSharingViolation(new AggregateException(new InvalidOperationException("other"), held)));
    }

    [TestMethod]
    [DataRow(unchecked((int)0x80070020), DisplayName = "ERROR_SHARING_VIOLATION")]
    [DataRow(unchecked((int)0x80070021), DisplayName = "ERROR_LOCK_VIOLATION")]
    public void The_windows_codes_are_recognised(int code) =>
        Assert.IsTrue(IoErrors.IsSharingViolation(new IOException("held", code)));

    [TestMethod]
    public void Other_failures_are_not_a_file_held()
    {
        Assert.IsFalse(IoErrors.IsSharingViolation(new IOException("denied", unchecked((int)0x80070005))));
        Assert.IsFalse(IoErrors.IsSharingViolation(new UnauthorizedAccessException("denied")));
        Assert.IsFalse(IoErrors.IsSharingViolation(new IOException("full", DiskFullPoints.Code(DiskFullError.Windows112))));
    }
}
