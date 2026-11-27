using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Routes;
using Autopilot.Core.Storage;
using Autopilot.Core.Tests.Net;
using Autopilot.Core.Tests.TestBeds;
using Microsoft.Extensions.Logging.Abstractions;

namespace Autopilot.Core.Tests.Install;

/// <summary>What a test reads back from a transaction bed: the record, the files, the steps a plan resolves to.</summary>
internal static class BedReads
{
    public static async Task<RecordedInstall?> FindAsync(this TransactionBed bed, CancellationToken cancellationToken) =>
        await Recorded.FindAsync(bed.Tree.PathOf(bed.InstallFolder), cancellationToken);

    /// <summary>The record, which the test expects to be there and readable.</summary>
    public static async Task<InstallRecord> RecordAsync(this TransactionBed bed, CancellationToken cancellationToken) =>
        (await bed.FindAsync(cancellationToken))?.Record ?? throw new AssertFailedException("no readable record");

    /// <summary>The record's entries as <c>path state</c>, joined by <c>|</c>.</summary>
    public static async Task<string> EntriesAsync(this TransactionBed bed, CancellationToken cancellationToken) =>
        string.Join("|", (await bed.RecordAsync(cancellationToken)).Entries.Select(entry => $"{entry.Path} {entry.State}"));

    /// <summary>The steps the resolver predicts for <paramref name="plan"/> over the folder as it is now.</summary>
    public static async Task<IReadOnlyList<WriteStep>> ResolvedStepsAsync(this TransactionBed bed, InstallPlan plan, CancellationToken cancellationToken)
    {
        RecordedInstall? recorded = await bed.FindAsync(cancellationToken);
        return (await new InstallResolver(NullLogger.Instance).ResolveAsync(plan, recorded, cancellationToken)).Steps();
    }

    /// <summary>The steps a real run of <paramref name="plan"/> makes over a copy of the folder as it is now.</summary>
    public static async Task<IReadOnlyList<WriteStep>> MadeStepsAsync(this TransactionBed bed, InstallPlan plan, CancellationToken cancellationToken)
    {
        using TransactionBed copy = bed.Clone();
        var made = CrashPoints.After(int.MaxValue);
        copy.Steps = made;
        _ = await copy.InstallAsync(plan with { GameFolder = copy.Tree.Root, InstallFolder = copy.Tree.PathOf(copy.InstallFolder) }, cancellationToken);
        return made.Made;
    }

    /// <summary>The commit's step, counted from 1, in a real run of <paramref name="plan"/> over a copy of the folder as it is now.</summary>
    public static async Task<int> CommitStepAsync(this TransactionBed bed, InstallPlan plan, CancellationToken cancellationToken) =>
        TransactionSteps.CommitStep(await bed.MadeStepsAsync(plan, cancellationToken));

    /// <summary>Whether the file at <paramref name="relative"/> holds exactly <paramref name="content"/>.</summary>
    public static bool Holds(this TransactionBed bed, string relative, byte[] content) =>
        File.Exists(bed.Tree.PathOf(relative)) && File.ReadAllBytes(bed.Tree.PathOf(relative)).SequenceEqual(content);

    public static bool Exists(this TransactionBed bed, string relative) => File.Exists(bed.Tree.PathOf(relative));

    /// <summary>The held files' names, sorted and joined by <c>|</c>.</summary>
    public static string HeldFiles(this TransactionBed bed) =>
        string.Join("|", Directory.EnumerateFiles(bed.Tree.Root, "*" + KnownFiles.HeldSuffix, SearchOption.AllDirectories).Select(Path.GetFileName).Order(StringComparer.Ordinal));

    /// <summary>Whether <c>dxgi.dll</c>'s backup is the game's file of <see cref="Switches"/>: its bytes, read-only attribute and time.</summary>
    public static bool BackupIsTheOriginal(this TransactionBed bed)
    {
        string backup = "dxgi.dll" + KnownFiles.BackupSuffix;
        return bed.Holds(backup, Switches.GameDxgi)
            && File.GetAttributes(bed.Tree.PathOf(backup)).HasFlag(FileAttributes.ReadOnly)
            && File.GetLastWriteTimeUtc(bed.Tree.PathOf(backup)) == Switches.GameTime.UtcDateTime;
    }

    /// <summary>Whether a folder is at <paramref name="relative"/>.</summary>
    public static bool HasFolder(this TransactionBed bed, string relative) => Directory.Exists(bed.Tree.PathOf(relative));

    /// <summary>The file at <paramref name="relative"/>, read whole.</summary>
    public static string Read(this TransactionBed bed, string relative) => File.ReadAllText(bed.Tree.PathOf(relative));
}

/// <summary>Plans and plan steps as tests write them.</summary>
internal static class Plans
{
    /// <summary>A plan of one group, on <paramref name="route"/>, in the bed's game folder.</summary>
    public static InstallPlan Plan(this TransactionBed bed, RouteId route, params PlanStep[] steps) => bed.Plan(route, new PlanGroup("Component", steps));

    public static InstallPlan Plan(this TransactionBed bed, RouteId route, params PlanGroup[] groups) => new(
        bed.Tree.Root,
        bed.Tree.PathOf(bed.InstallFolder),
        new PlanHeader(
            "1.0.0-test",
            new RecordedGame(["Game.exe"], Bitness.Bits64, GraphicsApi.DX12, GameSource.Steam),
            route,
            RouteSettings.Default,
            new Dictionary<string, string> { ["component"] = "1.0" },
            [$"installed {route.Id()}"],
            [],
            []),
        groups);

    /// <summary>A file placed from generated bytes.</summary>
    public static PlaceFile Place(string path, byte[] content, EntryClass kind = EntryClass.Component) =>
        new(path, kind, Facts(content), new GeneratedSource(content));

    public static FileFacts Facts(byte[] content) => new(CacheFixtures.DigestOf(content), content.Length);
}
