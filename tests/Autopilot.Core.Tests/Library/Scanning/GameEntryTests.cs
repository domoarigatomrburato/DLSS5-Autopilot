using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;
using Autopilot.Core.Library;
using Autopilot.Core.Library.Stores;

namespace Autopilot.Core.Tests.Library.Scanning;

/// <summary>What an entry says about itself: whether it can be set up, and whether Windows keeps its folder.</summary>
[TestClass]
public sealed class GameEntryTests
{
    private static readonly GameEntry _readable = new()
    {
        Name = "Game",
        Folder = @"D:\Games\Game",
        Source = GameSource.Steam,
        Executable = @"D:\Games\Game\Game.exe",
        Bitness = Bitness.Bits64,
        Api = GraphicsApi.DX11,
    };

    public static IEnumerable<(GameEntry Entry, string? Unsupported)> Verdicts =>
    [
        (_readable, null),
        (_readable with { Executable = null, Error = new GameReadError(GameReadFailure.NoExecutable, "no executable found") }, "No game executable found."),
        (_readable with { Error = new GameReadError(GameReadFailure.Unreadable, "Game.exe has no PE header.") }, "Game.exe has no PE header."),
        (_readable with { Protected = true, Bitness = null }, "Choose this game's 'architecture' and 'graphics api' in the game's settings - Windows protects its executable, so they cannot be read from it."),
        (_readable with { Protected = true, Api = GraphicsApi.Unknown }, "Choose this game's 'architecture' and 'graphics api' in the game's settings - Windows protects its executable, so they cannot be read from it."),
        (_readable with { Protected = true, Api = GraphicsApi.Dxgi }, "Choose this game's 'architecture' and 'graphics api' in the game's settings - Windows protects its executable, so they cannot be read from it."),
        (_readable with { Protected = true }, null),
        (_readable with { Bitness = null }, "Could not read the architecture."),
        (_readable with { Api = GraphicsApi.DX8 }, "DirectX 8 is 32-bit only, and this executable is 64-bit - set 'graphics api' to the one it really draws with in the game's settings."),
        (_readable with { Api = GraphicsApi.DX8, Bitness = Bitness.Bits32 }, null),
    ];

    [TestMethod]
    [DynamicData(nameof(Verdicts))]
    public void An_entry_says_why_it_cannot_be_set_up(GameEntry entry, string? unsupported) =>
        Assert.AreEqual(unsupported, entry.Unsupported);

    [TestMethod]
    [DataRow(@"C:\XboxGames\Forza\Content", true)]
    [DataRow(@"C:\Program Files\WindowsApps\Forza_1.0_x64", true)]
    [DataRow(@"D:\xboxgames\Halo", true)]
    [DataRow("/mnt/c/XboxGames/Halo", true)]
    [DataRow(@"C:\Program Files\ModifiableWindowsApps\Forza", false)]
    [DataRow(@"D:\MyXboxGames\Halo", false)]
    [DataRow(@"D:\Games\Halo", false)]
    public void Only_a_folder_segment_named_XboxGames_or_WindowsApps_is_a_locked_store_folder(string folder, bool locked) =>
        Assert.AreEqual(locked, (_readable with { Folder = folder }).InLockedStoreFolder);
}
