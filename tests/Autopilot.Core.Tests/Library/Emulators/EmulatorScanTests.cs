using Autopilot.Core.Library;
using Autopilot.Core.Library.Emulators;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Platform;
using Autopilot.Core.Tests.Library.Scanning;
using Autopilot.Core.Tests.Library.Stores;

namespace Autopilot.Core.Tests.Library.Emulators;

/// <summary>Emulators in a whole scan: found by a full scan only, and never merged into one entry.</summary>
[TestClass]
public sealed class EmulatorScanTests(TestContext testContext) : IDisposable
{
    private readonly ScanBed _bed = new();

    private CancellationToken Ct => testContext.CancellationToken;

    public void Dispose() => _bed.Dispose();

    [TestMethod]
    public async Task Two_games_of_one_emulator_executable_are_both_listed()
    {
        _ = _bed.Exe("Lib/RetroArch/retroarch.exe");
        ScanRequest request = _bed.Full(_bed.Lists(GameSource.Steam, "Lib"), _bed.Lists(GameSource.Folder, "Lib/RetroArch"));

        LibraryScan scan = await Scans.ScanAsync(request, Ct);

        Assert.AreEqual("Lib (Steam), RetroArch (Folder)", Scans.Listed(scan));
    }

    [TestMethod]
    public async Task A_full_scan_finds_the_emulators_and_a_quick_scan_does_not_search_for_them()
    {
        _ = _bed.Exe("D/Emulators/RetroArch/retroarch.exe");
        EmulatorSource search = SearchingDrive("D");

        LibraryScan full = await Scans.ScanAsync(_bed.Full() with { FullScanOnlySources = [search] }, Ct);
        LibraryScan quick = await Scans.ScanAsync(_bed.Quick([]) with { FullScanOnlySources = [search] }, Ct);

        Assert.AreEqual("RetroArch (multi-system) (Emulator)", Scans.Listed(full));
        Assert.IsEmpty(quick.Games);
    }

    private EmulatorSource SearchingDrive(string drive) =>
        new(new FakeRegistry(), new FakeDrives(new DriveRoot(_bed.Folder(drive) + Path.DirectorySeparatorChar, DriveKind.Fixed)), new HostFolders(null, null, null, null, null));
}
