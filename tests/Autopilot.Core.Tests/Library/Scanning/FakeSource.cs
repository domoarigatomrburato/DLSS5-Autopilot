using Autopilot.Core.Library.Stores;

namespace Autopilot.Core.Tests.Library.Scanning;

/// <summary>A source that lists the games it is given, or fails as a whole.</summary>
internal sealed class FakeSource(GameSource source, Func<IReadOnlyList<StoreGame>> find) : IGameSource
{
    public GameSource Source => source;

    public bool Asked { get; private set; }

    public static FakeSource Listing(GameSource source, params StoreGame[] games) => new(source, () => games);

    public static FakeSource Failing(GameSource source, Exception failure) => new(source, () => throw failure);

    public Task<IReadOnlyList<StoreGame>> FindAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Asked = true;
        return Task.FromResult(find());
    }
}
