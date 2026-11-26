using Autopilot.Core.Platform;

namespace Autopilot.Core.Tests.Library.Stores;

/// <summary>The machine's drives, as folders of the test's temp tree.</summary>
internal sealed class FakeDrives(params DriveRoot[] drives) : IDrives
{
    public IReadOnlyList<DriveRoot> List() => drives;
}
