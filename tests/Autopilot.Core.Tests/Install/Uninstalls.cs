using Autopilot.Core.Install;
using Autopilot.Core.Tests.TestBeds;
using Microsoft.Extensions.Logging.Abstractions;

namespace Autopilot.Core.Tests.Install;

/// <summary>Uninstalls run on a transaction bed, with its steps, seam, clock and running programs.</summary>
internal static class Uninstalls
{
    /// <summary>An uninstall over the bed; <paramref name="outside"/> undoes the changes outside the files (by default none is, as today).</summary>
    public static Uninstaller Uninstaller(this TransactionBed bed, IOutsideChanges? outside = null) =>
        new(new InstallRecords(NullLogger<InstallRecords>.Instance, bed.Time), bed.Store, FakeFreeSpace.OneDrive, bed.Processes, bed.Time, bed.Log, bed.Steps, bed.Seam, outside ?? OutsideChanges.NotUndoneYet);

    /// <summary>Uninstalls the bed's install folder.</summary>
    public static Task<UninstallOutcome> UninstallAsync(this TransactionBed bed, CancellationToken cancellationToken, IOutsideChanges? outside = null) =>
        bed.Uninstaller(outside).RunAsync(bed.Tree.Root, bed.Tree.PathOf(bed.InstallFolder), "1.0.0-test", cancellationToken);

    /// <summary>Uninstalls <paramref name="installFolder"/> in <paramref name="gameFolder"/>, both relative to the bed's root.</summary>
    public static Task<UninstallOutcome> UninstallAsync(this TransactionBed bed, string gameFolder, string installFolder, CancellationToken cancellationToken) =>
        bed.Uninstaller().RunAsync(bed.Tree.PathOf(gameFolder), bed.Tree.PathOf(installFolder), "1.0.0-test", cancellationToken);

    /// <summary>Uninstalls, which the test expects to end as <typeparamref name="T"/>.</summary>
    public static async Task<T> UninstallAsAsync<T>(this TransactionBed bed, CancellationToken cancellationToken, IOutsideChanges? outside = null)
        where T : UninstallOutcome
    {
        UninstallOutcome outcome = await bed.UninstallAsync(cancellationToken, outside);
        return outcome as T ?? throw new AssertFailedException($"the uninstall ended as {outcome}, not {typeof(T).Name}");
    }

    /// <summary>Uninstalls, which the test expects to run to its end.</summary>
    public static Task<UninstallFinished> UninstallFinishedAsync(this TransactionBed bed, CancellationToken cancellationToken, IOutsideChanges? outside = null) =>
        bed.UninstallAsAsync<UninstallFinished>(cancellationToken, outside);
}
