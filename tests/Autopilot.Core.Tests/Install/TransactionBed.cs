using System.Runtime.CompilerServices;
using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Platform;
using Autopilot.Core.State;
using Autopilot.Core.Storage;
using Autopilot.Core.Tests.TestBeds;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;

namespace Autopilot.Core.Tests.Install;

/// <summary>
/// A game folder, the tool's data folder with its per-game state, and install transactions over them: plans
/// built by hand, run with crash, full-drive, cancel and lock points, and the record and note read back.
/// </summary>
internal sealed class TransactionBed : IDisposable
{
    private readonly GameTree _data;

    public TransactionBed(params IEnumerable<TreeFile> files)
        : this(GameTree.Of(files), new GameTree("invidia-claritatis"))
    {
    }

    private TransactionBed(GameTree tree, GameTree data)
    {
        Tree = tree;
        _data = data;
        Store = new GameStateStore(_data.Root, Time);
    }

    public GameTree Tree { get; }

    public RecordingTimers Time { get; } = new();

    public GameStateStore Store { get; }

    public FakeLogger Log { get; } = new();

    /// <summary>The install folder, relative to the game folder.</summary>
    public string InstallFolder { get; init; } = "";

    /// <summary>The failure note the folder has.</summary>
    public FailureNote? Note => FailureNotes.Read(Store, GameIdentity.Of(Tree.PathOf(InstallFolder)));

    /// <summary>Told before every change the next transaction makes: crash, full-drive, cancel and look points.</summary>
    public IWriteSteps Steps { get; set; } = NoWriteSteps.Instance;

    /// <summary>The renames and deletes transactions go through: hold a file with <see cref="HeldPaths"/>.</summary>
    public DiskSeam Seam { get; set; } = DiskSeam.Real;

    /// <summary>Where the next transaction reports its progress.</summary>
    public IProgress<InstallProgress>? Progress { get; set; }

    public InstallTransaction Transaction() =>
        new(new InstallRecords(NullLogger<InstallRecords>.Instance, Time), Store, new FakeFreeSpace(("", new DriveSpace("G:", 1024))), Time, Log, Steps, Seam);

    public Task<InstallOutcome> InstallAsync(InstallPlan plan, CancellationToken cancellationToken) =>
        Transaction().RunAsync(plan, Progress, cancellationToken);

    /// <summary>A new transaction's recovery of the folder, and nothing else, with every step going ahead.</summary>
    public Task<InstallOutcome?> RecoverAsync(CancellationToken cancellationToken)
    {
        Steps = NoWriteSteps.Instance;
        return Transaction().RecoverAsync(Tree.Root, Tree.PathOf(InstallFolder), "1.0.0-test", cancellationToken);
    }

    public TreeSnapshot Snapshot() => TreeSnapshot.Of(Tree.Root);

    /// <summary>A bed holding a copy of this one's game folder and data folder, attributes and times included: one installed template, many runs.</summary>
    public TransactionBed Clone()
    {
        TransactionBed clone = new(new GameTree(), new GameTree("invidia-claritatis")) { InstallFolder = InstallFolder };
        Copy(Tree.Root, clone.Tree.Root);
        Copy(_data.Root, clone._data.Root);
        return clone;
    }

    public void Dispose()
    {
        Tree.Dispose();
        _data.Dispose();
    }

    private static void Copy(string from, string to)
    {
        foreach (string folder in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
        {
            _ = Directory.CreateDirectory(Path.Join(to, Path.GetRelativePath(from, folder)));
        }

        foreach (string file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            string copy = Path.Join(to, Path.GetRelativePath(from, file));
            File.Copy(file, copy);
            File.SetLastWriteTimeUtc(copy, File.GetLastWriteTimeUtc(file));
            File.SetAttributes(copy, File.GetAttributes(file));
        }
    }
}


/// <summary>A file another program holds: renames from or to it, and deletes of it, fail with a sharing violation.</summary>
internal sealed class HeldPaths
{
    private readonly HashSet<string> _held = [with(StringComparer.OrdinalIgnoreCase)];

    public DiskSeam Seam => new(
        (from, to) =>
        {
            Check(from);
            Check(to);
            File.Move(from, to, overwrite: false);
        },
        path =>
        {
            Check(path);
            File.Delete(path);
        });

    public HeldPaths Hold(string path)
    {
        _ = _held.Add(Path.GetFullPath(path));
        return this;
    }

    public void Release(string path) => _held.Remove(Path.GetFullPath(path));

    private void Check(string path)
    {
        if (_held.Contains(Path.GetFullPath(path)))
        {
            throw new IOException($"The process cannot access the file '{path}' because it is being used by another process.", unchecked((int)0x80070020));
        }
    }
}

/// <summary>Write steps that run an action as a chosen step arrives, then let it go ahead.</summary>
internal sealed class StepHook(Func<int, WriteStep, bool> at, Action act) : IWriteSteps
{
    private int _steps;

    public void Before(WriteStep step)
    {
        if (at(++_steps, step))
        {
            act();
        }
    }
}

/// <summary>Two write steps in turn: a look at the folder, then a full drive, say.</summary>
internal sealed class BothSteps(IWriteSteps first, IWriteSteps second) : IWriteSteps
{
    public void Before(WriteStep step)
    {
        first.Before(step);
        second.Before(step);
    }
}

/// <summary>Where the commit falls among a transaction's steps.</summary>
internal static class TransactionSteps
{
    /// <summary>The commit's save among <paramref name="steps"/>, the steps of a whole run, counted from 1: the step that makes the install <see cref="RecordState.Installed"/>.</summary>
    public static int CommitStep(IReadOnlyList<WriteStep> steps)
    {
        int firstHeldDelete = steps.ToList().FindIndex(step => step.Kind is WriteKind.Delete or WriteKind.SetAttributes && step.Path.EndsWith(KnownFiles.HeldSuffix, StringComparison.Ordinal));
        return firstHeldDelete < 0 ? steps.Count : firstHeldDelete;
    }
}

/// <summary>Transactions run on a bed as tests expect them to end.</summary>
internal static class BedRuns
{
    /// <summary>Installs <paramref name="plan"/>, which the test expects to end as <typeparamref name="T"/>.</summary>
    public static async Task<T> InstallAsAsync<T>(this TransactionBed bed, InstallPlan plan, CancellationToken cancellationToken)
        where T : InstallOutcome
    {
        InstallOutcome outcome = await bed.InstallAsync(plan, cancellationToken);
        return outcome as T ?? throw new AssertFailedException($"the install ended as {outcome}, not {typeof(T).Name}");
    }

    /// <summary>Installs <paramref name="plan"/>, which the test expects to commit.</summary>
    public static Task<InstallCommitted> CommitAsync(this TransactionBed bed, InstallPlan plan, CancellationToken cancellationToken) => bed.InstallAsAsync<InstallCommitted>(plan, cancellationToken);

    /// <summary>Installs <paramref name="plan"/> with the drive filling at its commit, after <see cref="TransactionBed.Steps"/> saw each step.</summary>
    public static async Task<InstallFailed> FailAtCommitAsync(this TransactionBed bed, InstallPlan plan, CancellationToken cancellationToken)
    {
        bed.Steps = new BothSteps(bed.Steps, DiskFullPoints.At(await bed.CommitStepAsync(plan, cancellationToken)));
        return await bed.InstallAsAsync<InstallFailed>(plan, cancellationToken);
    }

    /// <summary>Has the next transaction run <paramref name="look"/> as <paramref name="plan"/>'s commit arrives; the box gets the answer.</summary>
    public static async Task<StrongBox<T?>> LookAtCommitAsync<T>(this TransactionBed bed, InstallPlan plan, Func<TransactionBed, T> look, CancellationToken cancellationToken)
    {
        int commit = await bed.CommitStepAsync(plan, cancellationToken);
        StrongBox<T?> seen = new();
        bed.Steps = new StepHook((step, _) => step == commit, () => seen.Value = look(bed));
        return seen;
    }
}
