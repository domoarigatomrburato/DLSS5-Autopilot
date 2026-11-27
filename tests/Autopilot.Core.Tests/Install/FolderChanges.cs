using Autopilot.Core.Platform;
using Autopilot.Core.Tests.State;

namespace Autopilot.Core.Tests.Install;

/// <summary>What happens to a bed's folder between an install and its uninstall: a store, a person, antivirus or another program at work.</summary>
internal static class FolderChanges
{
    /// <summary>The waits the bed's clock was asked for, in milliseconds, in order.</summary>
    public static int[] WaitsInMilliseconds(this TransactionBed bed) => [.. bed.Time.Waits.Select(wait => (int)wait.TotalMilliseconds)];

    /// <summary>Writes <paramref name="content"/> at <paramref name="relative"/>, read-only cleared first, as a store or a person would.</summary>
    public static void Write(this TransactionBed bed, string relative, byte[] content)
    {
        string path = bed.Tree.PathOf(relative);
        if (File.Exists(path))
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        TestFiles.Plant(path, content);
    }

    /// <summary>Adds a line to the file at <paramref name="relative"/>, as a component rewriting its config does.</summary>
    public static void Append(this TransactionBed bed, string relative) => File.AppendAllText(bed.Tree.PathOf(relative), "rewritten while running\r\n");

    /// <summary>Deletes the file at <paramref name="relative"/>, read-only cleared first, as antivirus would.</summary>
    public static void Delete(this TransactionBed bed, string relative)
    {
        string path = bed.Tree.PathOf(relative);
        File.SetAttributes(path, FileAttributes.Normal);
        File.Delete(path);
    }

    /// <summary>Plants a small file at each of <paramref name="paths"/>.</summary>
    public static void Plant(this TransactionBed bed, params string[] paths)
    {
        foreach (string path in paths)
        {
            TestFiles.Plant(bed.Tree.PathOf(path), "planted");
        }
    }

    /// <summary>The install folder's game, as the per-game state knows it.</summary>
    public static GameIdentity Game(this TransactionBed bed) => GameIdentity.Of(bed.Tree.PathOf(bed.InstallFolder));

    /// <summary>The attempts remembered in the game's state; 0 when none are.</summary>
    public static int Attempts(this TransactionBed bed) => bed.Store.Read(bed.Game()).Get("attempts", StateJson.Default.Int32);

    /// <summary>Remembers 3 attempts in the game's state, as a measurement the tool kept.</summary>
    public static void SetAttempts(this TransactionBed bed) => bed.Store.Update(bed.Game(), state => state.Set("attempts", 3, StateJson.Default.Int32), CancellationToken.None);

    /// <summary>Has the bed's next transaction cancel <paramref name="cancel"/> as its first delete comes, after which that delete goes ahead.</summary>
    public static void CancelAtFirstDelete(this TransactionBed bed, CancellationTokenSource cancel) =>
        bed.Steps = new StepHook((_, step) => step.Kind == Core.Storage.WriteKind.Delete, cancel.Cancel);

    /// <summary>Has another program hold the file at <paramref name="relative"/> from now on.</summary>
    public static HeldPaths Hold(this TransactionBed bed, string relative)
    {
        HeldPaths held = new HeldPaths().Hold(bed.Tree.PathOf(relative));
        bed.Seam = held.Seam;
        return held;
    }
}
