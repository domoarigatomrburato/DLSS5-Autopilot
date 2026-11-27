using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Library;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Routes;
using Autopilot.Core.Storage;
using Autopilot.Core.Tests.TestBeds;
using Microsoft.Extensions.Logging.Abstractions;

namespace Autopilot.Core.Tests.Install;

/// <summary>
/// A transaction bed with what the first phase of an install asks besides: the folder's access, the running
/// processes, the drive's room, and a request for a game read as a 64-bit Direct3D 12 <c>Game.exe</c>.
/// </summary>
internal sealed class RefusalBed(params IEnumerable<TreeFile> files) : IDisposable
{
    public TransactionBed Bed { get; } = new(files);

    public GameTree Tree => Bed.Tree;

    public FakeFolderAccess Access { get; } = new();

    public FakeProcesses Processes { get; } = new();

    /// <summary>The game's drive; a terabyte free unless the test says otherwise.</summary>
    public DriveSpace Drive { get; set; } = new("G:", 1L << 40);

    public InstallRefusals Refusals() =>
        new(new InstallRecords(NullLogger<InstallRecords>.Instance, Bed.Time), Access, Processes, new FakeFreeSpace(("", Drive)), NullLogger.Instance);

    /// <summary>The game as the library read it: <c>Game.exe</c> in the game folder, 64-bit, Direct3D 12.</summary>
    public GameEntry Game => new()
    {
        Name = "Game",
        Folder = Tree.Root,
        Source = GameSource.Steam,
        Executable = Tree.PathOf("Game.exe"),
        Candidates = [Tree.PathOf("Game.exe")],
        Bitness = Bitness.Bits64,
        Api = GraphicsApi.DX12,
    };

    /// <summary>A request to install <paramref name="plan"/>'s route, stable and without warnings, with <paramref name="settings"/>; the card not read.</summary>
    public RefusalRequest Request(InstallPlan plan, RouteSettings? settings = null) =>
        new(Game, RefusalGames.Option(plan.Header.Route), RefusalGames.Facts, settings ?? RouteSettings.Default, plan);

    public Task<InstallCheck> CheckAsync(RefusalRequest request, CancellationToken cancellationToken) => Refusals().CheckAsync(request, cancellationToken);

    public void Dispose() => Bed.Dispose();
}

/// <summary>The game and the route as a request names them.</summary>
internal static class RefusalGames
{
    /// <summary>The game as the routes read it: <c>Game.exe</c>, Direct3D 12, 64-bit, nothing shipped of its own, the card not read.</summary>
    public static RouteFacts Facts => RouteFactsBeds.Game(executable: "Game.exe");

    public static RouteOption Option(RouteId route, RouteReliability reliability = RouteReliability.Stable) => new(route, true, [], reliability, []);
}

/// <summary>What a test does to a refusal bed besides checking.</summary>
internal static class RefusalBedRuns
{
    /// <summary>A request for <paramref name="route"/> placing one add-on (or <paramref name="placed"/>) in the game folder, with <paramref name="settings"/>.</summary>
    public static RefusalRequest Request(this RefusalBed bed, RouteId route, RouteSettings? settings = null, string placed = "some.addon64") =>
        bed.Request(bed.Bed.Plan(route, Plans.Place(placed, GameFolders.Dll("ours"))), settings);

    /// <summary>Holds <paramref name="relative"/> open for reading, as a launcher holds the proxy DLL, until disposed.</summary>
    public static FileStream Hold(this RefusalBed bed, string relative) => new(bed.Tree.PathOf(relative), FileMode.Open, FileAccess.Read, FileShare.Read);

    /// <summary>Holds <paramref name="relative"/> open with no sharing, so no other reader can open it, until disposed.</summary>
    public static FileStream Lock(this RefusalBed bed, string relative) => new(bed.Tree.PathOf(relative), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

    /// <summary>The preview of <paramref name="request"/>'s install.</summary>
    public static async Task<InstallPreview> PreviewAsync(this RefusalBed bed, RefusalRequest request, CancellationToken cancellationToken) =>
        InstallPreview.Of(await bed.CheckAsync(request, cancellationToken));
}

/// <summary>What a test reads out of a check.</summary>
internal static class CheckReads
{
    /// <summary>The kinds of the blockers, in order, joined by <c>, </c>.</summary>
    public static string Kinds(this InstallCheck check) => string.Join(", ", check.Blockers.Select(blocker => blocker.Kind));

    /// <summary>The text of the one blocker of <paramref name="kind"/>, which the test expects.</summary>
    public static string Only(this InstallCheck check, BlockerKind kind) =>
        check.Blockers.SingleOrDefault(blocker => blocker.Kind == kind)?.Text ?? throw new AssertFailedException($"no {kind} blocker: {check.Kinds()}");

    /// <summary>Checks the one blocker of <paramref name="kind"/> says <paramref name="part"/>.</summary>
    public static void AssertSays(this InstallCheck check, BlockerKind kind, string part)
    {
        string text = check.Only(kind);
        Assert.IsTrue(text.Contains(part, StringComparison.Ordinal), $"the {kind} blocker says: {text}");
    }

    /// <summary>Checks nothing stops the install.</summary>
    public static void AssertAllowed(this InstallCheck check) => Assert.IsTrue(check.CanInstall, check.Kinds());
}
