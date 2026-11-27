using Autopilot.Core.Platform;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Platform;

/// <summary>An install refuses while the game runs: a process runs from inside the game's folders, compared as Windows compares paths.</summary>
[TestClass]
public sealed class RunningGameTests
{
    private const string Game = @"C:\Games\Foo";

    [TestMethod]
    [DataRow(@"C:\Games\Foo\Foo.exe", DisplayName = "beside the install folder")]
    [DataRow(@"C:\Games\Foo\Binaries\Win64\Foo-Win64-Shipping.exe", DisplayName = "below it")]
    [DataRow(@"c:\games\FOO\foo.exe", DisplayName = "another case")]
    [DataRow("C:/Games/Foo/Foo.exe", DisplayName = "forward slashes")]
    [DataRow(@"C:\Games\Foo\.\bin\..\Foo.exe", DisplayName = "dots spelled in")]
    public void A_process_from_inside_the_folder_is_the_game(string image)
    {
        FakeProcesses processes = new FakeProcesses().Run(image);

        Assert.HasCount(1, RunningGames.Inside(processes.List(), [Game]));
    }

    [TestMethod]
    [DataRow(@"C:\Games\Foo-other\x.exe", DisplayName = "a folder whose name starts the same")]
    [DataRow(@"C:\Games\Foo.exe", DisplayName = "a file named like the folder")]
    [DataRow(@"C:\Games\Foo\..\Bar\x.exe", DisplayName = "climbing out")]
    [DataRow(@"D:\Games\Foo\Foo.exe", DisplayName = "another drive")]
    [DataRow(@"C:\Games\x.exe", DisplayName = "the parent folder")]
    public void A_process_from_outside_the_folder_is_not(string image)
    {
        FakeProcesses processes = new FakeProcesses().Run(image);

        Assert.IsEmpty(RunningGames.Inside(processes.List(), [Game]));
    }

    [TestMethod]
    public void Every_folder_of_the_game_is_searched_and_an_unreadable_image_is_never_the_game()
    {
        FakeProcesses processes = new FakeProcesses()
            .Run(null)
            .Run(@"C:\Windows\explorer.exe")
            .Run(@"C:\Games\Foo\Engine\Foo.exe")
            .Run(@"\\server\games\Bar\Bar.exe");

        IReadOnlyList<RunningProcess> running = RunningGames.Inside(processes.List(), [@"C:\Games\Foo\Engine", @"\\SERVER\games\bar\"]);

        Assert.AreEqual(@"C:\Games\Foo\Engine\Foo.exe | \\server\games\Bar\Bar.exe", string.Join(" | ", running.Select(process => process.ImagePath)));
    }

    [TestMethod]
    public void A_drive_root_is_not_climbed_out_of() =>
        Assert.HasCount(1, RunningGames.Inside(new FakeProcesses().Run(@"C:\..\Games\Foo\Foo.exe").List(), [Game]));
}
