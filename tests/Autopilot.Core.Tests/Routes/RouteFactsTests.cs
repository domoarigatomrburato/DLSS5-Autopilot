using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;
using Autopilot.Core.Library;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.Routes;

/// <summary>The game and the card as the routes read them, built from what the library read and the card.</summary>
[TestClass]
public sealed class RouteFactsTests
{
    [TestMethod]
    public void The_facts_take_the_games_api_executable_architecture_and_candidates_and_the_card_as_read()
    {
        GameEntry game = new()
        {
            Name = "Game",
            Folder = @"C:\Games\Game",
            Source = GameSource.Steam,
            Executable = @"C:\Games\Game\bin\Game.exe",
            Candidates = [@"C:\Games\Game\bin\Game.exe", @"C:\Games\Game\Launcher.exe"],
            Bitness = Bitness.Bits32,
            Api = GraphicsApi.DX11,
            Protected = true,
        };

        var facts = RouteFacts.Of(game, RouteGames.Runtimes(ownDlss: true), RouteFactsBeds.TwoCards, new(IsReEngine: true, DlssBesideExecutable: true, LoaderNameFree: true));

        Assert.AreEqual(
            "DX11 Game.exe Bits32 read:False candidates:2 re:True beside:True loader:True two:True",
            $"{facts.Api} {facts.ExecutableName} {facts.Bitness} read:{facts.ArchitectureRead} candidates:{facts.Executables} re:{facts.IsReEngine} beside:{facts.DlssBesideExecutable} loader:{facts.LoaderNameFree} two:{facts.Gpu?.TwoGpus}");
        Assert.IsTrue(facts.Runtimes.OwnDlss);
    }

    [TestMethod]
    [DataRow(false, 64, true, DisplayName = "read from the executable")]
    [DataRow(false, 0, false, DisplayName = "not protected, yet not read")]
    [DataRow(true, 64, false, DisplayName = "protected, set by hand")]
    [DataRow(true, 0, false, DisplayName = "protected, not set")]
    public void The_architecture_counts_as_read_only_when_the_executable_said_it(bool isProtected, int bits, bool read)
    {
        GameEntry game = new() { Name = "Game", Folder = "Game", Source = GameSource.Steam, Executable = "Game/Game.exe", Protected = isProtected, Bitness = RouteGames.Bits(bits) };

        var facts = RouteFacts.Of(game, RouteGames.Runtimes(), null, new(false, false, false));

        Assert.AreEqual(read, facts.ArchitectureRead);
        Assert.AreEqual(!read, ShownSettings.For(RouteId.Native, facts, RouteSettings.Default).Contains(RouteSetting.Architecture));
    }
}
