using System.Text;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.Sources;
using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Install.Plans;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.Executables;
using Autopilot.Core.Tests.TestBeds;
using Microsoft.Extensions.Logging.Abstractions;

namespace Autopilot.Core.Tests.Install.Plans;

/// <summary>
/// A game folder (<c>Game.exe</c> plus what the test adds), Windows' own folder, and the files a gather would have put in
/// the cache: the plan of a route over the folder as surveyed, its preview, its install.
/// </summary>
/// <param name="files">What the game folder holds besides <c>Game.exe</c>.</param>
internal sealed class PlanBed(params IEnumerable<TreeFile> files) : IDisposable
{
    private readonly GameTree _system = new("System32");
    private readonly CachedComponents _cache = new();

    public RefusalBed Refusal { get; } = new([TreeFile.Bytes("Game.exe", GameFolders.Dll("the game")), .. files]);

    public TransactionBed Bed => Refusal.Bed;

    public GameTree Tree => Bed.Tree;

    /// <summary>The game as the routes read it; its runtimes are read from the folder when the plan is made.</summary>
    public RouteFacts Facts { get; set; } = RouteFactsBeds.Game(executable: "Game.exe");

    public RouteSettings Settings { get; set; } = RouteSettings.Default;

    /// <summary>What the gather put in the cache: every component's files, version <c>1.0</c>.</summary>
    public GatheredFiles Files => _cache.Files;

    /// <summary>The folder as the last plan surveyed it.</summary>
    public FolderSurvey? Survey { get; private set; }

    /// <summary>Windows' own <c>d3dcompiler_47.dll</c>, stamped with <paramref name="version"/>.</summary>
    public PlanBed WindowsCompiler(string version)
    {
        File.WriteAllBytes(Path.Join(_system.Root, KnownFiles.Compiler), PlanRuns.Compiler(version));
        return this;
    }

    /// <summary>The plan of <paramref name="route"/> over the folder as it is now, placing <paramref name="files"/> (the gathered ones by default).</summary>
    public async Task<InstallPlan> PlanAsync(CancellationToken cancellationToken, PlanFiles? files = null, RouteId route = RouteId.Native)
    {
        Survey = await SurveyAsync(cancellationToken);
        return RoutePlans.For(Request(route), Survey, files ?? Files);
    }

    // The record, the game's runtimes as the reader finds them with it (whether its own nvngx_dlss.dll is beside the
    // executable among them), and the folder as the survey reads it.
    private async Task<FolderSurvey> SurveyAsync(CancellationToken cancellationToken)
    {
        RecordedInstall? recorded = await new InstallRecords(NullLogger<InstallRecords>.Instance, Bed.Time).FindAsync(Tree.PathOf(Bed.InstallFolder), cancellationToken);
        Facts = await PlanRuns.WithRuntimesAsync(Facts, Tree, Bed.InstallFolder, recorded, cancellationToken);
        return await FolderSurvey.ReadAsync(new(Tree.Root, Tree.PathOf(Bed.InstallFolder), recorded, _system.Root), cancellationToken);
    }

    /// <summary>The game read with an NVIDIA card beside the processor's graphics: the plan sets the graphics preference.</summary>
    public PlanBed WithTwoCards()
    {
        Facts = RouteFactsBeds.Game(executable: "Game.exe") with { Gpu = RouteFactsBeds.TwoCards };
        return this;
    }

    public PlanRequest Request(RouteId route = RouteId.Native) =>
        new(Tree.Root, Tree.PathOf(Bed.InstallFolder), "Game.exe", route, Facts, Settings, new(["Game.exe"], Bitness.Bits64, Facts.Api, GameSource.Steam), "1.0.0-test");

    public void Dispose()
    {
        Refusal.Dispose();
        _system.Dispose();
        _cache.Dispose();
    }
}

/// <summary>What a test reads out of a plan.</summary>
internal static class PlanReads
{
    /// <summary>Every step, one per line, as <c>group: what path</c>.</summary>
    public static string Lines(this InstallPlan plan) =>
        string.Join("\n", plan.Groups.SelectMany(group => group.Steps.Select(step => $"{group.Name}: {Line(step)}")));

    /// <summary>The steps of the group named <paramref name="name"/>, joined by <c>, </c>; empty when the plan has no such group.</summary>
    public static string Group(this InstallPlan plan, string name) =>
        string.Join(", ", plan.Groups.Where(group => group.Name == name).SelectMany(group => group.Steps).Select(Line));

    /// <summary>The header's warnings, joined by <c> | </c>.</summary>
    public static string Warnings(this InstallPlan plan) => string.Join(" | ", plan.Header.Warnings);

    /// <summary>The groups' names, in order, one per line.</summary>
    public static string GroupNames(this InstallPlan plan) => string.Join("\n", plan.Groups.Select(group => group.Name));

    /// <summary>The manifest the Vulkan layer step writes.</summary>
    public static byte[] Manifest(this InstallPlan plan) => plan.Groups.SelectMany(group => group.Steps).OfType<RegisterLayer>().Single().Manifest.ToArray();

    /// <summary>The bytes <paramref name="path"/> is placed from.</summary>
    public static byte[] Placed(this InstallPlan plan, string path) =>
        plan.Groups.SelectMany(group => group.Steps).OfType<PlaceFile>().Single(step => step.Path == path).Source switch
        {
            GeneratedSource generated => generated.Bytes.ToArray(),
            CachedSource cached => File.ReadAllBytes(cached.Path),
            FileSource other => throw new AssertFailedException($"{path} is placed from {other}"),
        };

    public static string Text(this InstallPlan plan, string path) => Encoding.UTF8.GetString(plan.Placed(path));

    private static string Line(PlanStep step) => step switch
    {
        PlaceFile place => $"place {place.Path}",
        MoveAside aside => $"aside {aside.Path} ({aside.Backup})",
        KeepEntry keep => $"keep {keep.Path}",
        RegisterLayer layer => $"layer {layer.Layer}",
        SetGraphicsPreference preference => $"preference {preference.Path}",
        _ => $"{step.GetType().Name} {step.Path}",
    };
}

/// <summary>The files a gather would have put in the cache: every component's, version <c>1.0</c>.</summary>
internal sealed class CachedComponents : IDisposable
{
    private static readonly (string Id, string File)[] _components =
    [
        .. ReShadeInstaller.Files.Select(file => (ComponentIds.ReShade, file)),
        (ComponentIds.Renodx, KnownFiles.RenodxAddon),
        (ComponentIds.Dlssnr, KnownFiles.NeuralRendering),
        (ComponentIds.Dlss, KnownFiles.Dlss),
        (ComponentIds.Dlssd, KnownFiles.RayReconstruction),
    ];

    private readonly GameTree _cache = new("cache");

    public CachedComponents()
    {
        foreach ((string id, string file) in _components)
        {
            string path = Path.Join(_cache.Root, id, file);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, Bytes(id, file));
            Files.Add(id, file, path, FileFacts.Of(Bytes(id, file)), Bytes(id, file));
            Files.Version(id, "1.0");
        }
    }

    public GatheredFiles Files { get; } = new();

    /// <summary>The bytes the gather fetched for <paramref name="file"/> of <paramref name="id"/>.</summary>
    public static byte[] Bytes(string id, string file) =>
        file.EndsWith(".json", StringComparison.Ordinal) ? Encoding.UTF8.GetBytes($$"""{"layer":"{{file}}"}""") : GameFolders.Dll($"{id} {file}");

    public void Dispose() => _cache.Dispose();
}

/// <summary>What a test does with a plan bed besides planning.</summary>
internal static class PlanRuns
{
    /// <summary>
    /// <paramref name="facts"/> with the game's runtimes as the reader finds them in <paramref name="tree"/> with the record, and
    /// whether its own <c>nvngx_dlss.dll</c> is among them beside the executable.
    /// </summary>
    public static async Task<RouteFacts> WithRuntimesAsync(RouteFacts facts, GameTree tree, string installFolder, RecordedInstall? recorded, CancellationToken cancellationToken)
    {
        GameRuntimes runtimes = await GameRuntimesReader.ReadAsync(tree.PathOf("Game.exe"), tree.Root, null, recorded, cancellationToken);
        string beside = RecordPaths.Key(Path.Join(installFolder, KnownFiles.Dlss));
        return facts with
        {
            Runtimes = runtimes,
            DlssBesideExecutable = runtimes.DlssEvidence.Any(path => string.Equals(RecordPaths.Key(path), beside, StringComparison.OrdinalIgnoreCase)),
        };
    }

    /// <summary>A <c>d3dcompiler_47.dll</c> stamped with <paramref name="version"/>.</summary>
    public static byte[] Compiler(string version) => new TestPe().WithVersion(new VersionBlock(version)).ToBytes();

    /// <summary>The plan's install, as the transaction runs it.</summary>
    public static async Task<InstallOutcome> InstallAsync(this PlanBed bed, CancellationToken cancellationToken) => await bed.Bed.InstallAsync(await bed.PlanAsync(cancellationToken), cancellationToken);

    /// <summary>The first phase's check of <paramref name="plan"/>, the survey passed so a taken proxy name is refused.</summary>
    public static Task<InstallCheck> CheckAsync(this PlanBed bed, InstallPlan plan, CancellationToken cancellationToken) =>
        bed.Refusal.CheckAsync(new RefusalRequest(bed.Refusal.Game, RefusalGames.Option(plan.Header.Route), bed.Facts, bed.Settings, plan, bed.Survey), cancellationToken);

    /// <summary>The first phase's blockers over the plan of the folder as it is, as <c>kind: text</c> lines; empty when none.</summary>
    public static async Task<string> RefusedAsync(this PlanBed bed, CancellationToken cancellationToken) =>
        await bed.RefusedAsync(await bed.PlanAsync(cancellationToken), cancellationToken);

    /// <summary>The first phase's blockers over <paramref name="plan"/>, as <c>kind: text</c> lines; empty when none.</summary>
    public static async Task<string> RefusedAsync(this PlanBed bed, InstallPlan plan, CancellationToken cancellationToken)
    {
        InstallCheck check = await bed.CheckAsync(plan, cancellationToken);
        return string.Join("\n", check.Blockers.Select(blocker => $"{blocker.Kind}: {blocker.Text}"));
    }

    /// <summary>What the preview of <paramref name="plan"/> lists: the files written, then those backed up.</summary>
    public static async Task<string> PreviewedAsync(this PlanBed bed, InstallPlan plan, CancellationToken cancellationToken)
    {
        var preview = InstallPreview.Of(await bed.CheckAsync(plan, cancellationToken));
        return string.Join(", ", preview.Written.Select(file => file.Path)) + " | " + string.Join(", ", preview.BackedUp);
    }
}

/// <summary>The places outside the game folder a plan bed can give a plan.</summary>
internal static class PlanBedPlaces
{
    /// <summary>Gives the bed empty registries and the layer folder in its data folder, so a plan may register a layer.</summary>
    public static void WithLayerFolder(this PlanBed bed) =>
        bed.Bed.Shared = new SharedPlaces(new FakeUserRegistry(), new Library.Stores.FakeRegistry(), Path.Join(bed.Bed.DataRoot, "reshade-vulkan"));
}
