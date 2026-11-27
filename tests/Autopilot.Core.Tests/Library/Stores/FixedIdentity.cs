using Autopilot.Core.Platform;

namespace Autopilot.Core.Tests.Library.Stores;

/// <summary>A machine whose hardware identity is the one given; null when it cannot tell.</summary>
internal sealed class FixedIdentity(HardwareIdentity? identity) : IHardwareIdentity
{
    public Task<HardwareIdentity?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(identity);
}
