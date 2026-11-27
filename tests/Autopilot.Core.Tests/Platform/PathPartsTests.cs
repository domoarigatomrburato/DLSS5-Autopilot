using Autopilot.Core.Platform;

namespace Autopilot.Core.Tests.Platform;

/// <summary>Which paths are a root, by Windows' rules on every host: a drive, a UNC server or share, the Unix root.</summary>
[TestClass]
public sealed class PathPartsTests
{
    [TestMethod]
    [DataRow(@"C:\")]
    [DataRow("C:")]
    [DataRow("c:/")]
    [DataRow(@"\\server")]
    [DataRow(@"\\server\share")]
    [DataRow(@"\\server\share\")]
    [DataRow("//server/share")]
    [DataRow("/")]
    [DataRow(@"C:\Games\..")]
    public void A_drive_a_unc_server_or_share_and_the_unix_root_are_roots(string path) => Assert.IsTrue(PathParts.IsRoot(path));

    [TestMethod]
    [DataRow(@"C:\Games")]
    [DataRow(@"\\server\share\Games")]
    [DataRow("/Games")]
    public void A_folder_below_a_root_is_not_one(string path) => Assert.IsFalse(PathParts.IsRoot(path));
}
