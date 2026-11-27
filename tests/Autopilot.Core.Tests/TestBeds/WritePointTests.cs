using Autopilot.Core.Platform;
using Autopilot.Core.Storage;

namespace Autopilot.Core.Tests.TestBeds;

/// <summary>Crash points, disk-full points and the platform fakes, the beds the install tests stand on, do what they say.</summary>
[TestClass]
public sealed class WritePointTests
{
    [TestMethod]
    public void A_crash_point_lets_n_steps_through_and_fails_every_later_one()
    {
        var crash = CrashPoints.After(2);
        WriteStep[] steps = [new(WriteKind.SaveRecord, "record"), new(WriteKind.Move, "dxgi.dll", "dxgi.dll.bak"), new(WriteKind.Create, "dxgi.dll"), new(WriteKind.Delete, "dxgi.dll.bak")];

        crash.Before(steps[0]);
        crash.Before(steps[1]);

        Assert.AreEqual(steps[2], Assert.ThrowsExactly<SimulatedCrash>(() => crash.Before(steps[2])).Step);
        _ = Assert.ThrowsExactly<SimulatedCrash>(() => crash.Before(steps[3]), "a rollback after the crash fails too");
        CollectionAssert.AreEqual(steps[..2], crash.Made.ToArray());
    }

    [TestMethod]
    [DataRow(nameof(DiskFullError.Windows112), false)]
    [DataRow(nameof(DiskFullError.Windows39), true)]
    [DataRow(nameof(DiskFullError.Errno28), true)]
    public void A_disk_full_point_fails_its_step_once_with_an_error_the_tool_reads_as_a_full_drive(string kind, bool wrapped)
    {
        DiskFullError error = Enum.Parse<DiskFullError>(kind);
        var full = DiskFullPoints.At(2, error, wrapped);
        WriteStep step = new(WriteKind.Copy, "dxgi.dll", "dxgi.dll.bak");

        full.Before(step);
        Exception failure = Assert.Throws<Exception>(() => full.Before(step));
        full.Before(step);

        Assert.AreEqual(wrapped, failure is not IOException);
        Assert.AreEqual(error != DiskFullError.Errno28 || !OperatingSystem.IsWindows(), IoErrors.IsDiskFull(failure), "errno 28 is a full drive off Windows only");
    }

    [TestMethod]
    public void The_production_write_steps_let_every_step_through() =>
        NoWriteSteps.Instance.Before(new(WriteKind.Replace, "dxgi.dll"));

    [TestMethod]
    public void The_platform_fakes_answer_as_set()
    {
        using GameTree game = new();
        FakeFolderAccess access = new FakeFolderAccess().Deny(game.PathOf("Xbox") + Path.DirectorySeparatorChar);
        FakeElevation elevation = new() { IsElevated = true };

        Assert.AreEqual(FolderAccess.Writable, access.CanWrite(game.Root));
        Assert.AreEqual(FolderAccess.Denied, access.CanWrite(game.PathOf("Xbox")));
        Assert.AreEqual(FolderAccess.Missing, access.CanWrite(game.PathOf("gone")));
        Assert.IsTrue(elevation.IsElevated);
        Assert.HasCount(2, new FakeProcesses().Run(null).Run(game.PathOf("Game.exe")).List());
    }
}
