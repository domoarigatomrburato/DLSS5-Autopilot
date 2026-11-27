using Autopilot.Core.Net;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Autopilot.Core.Tests.Net;

/// <summary>A client wired to the fake server, a fake clock and a throwaway cache folder.</summary>
internal sealed class TestBed : IDisposable
{
    public static readonly HostAllowlist Hosts = new("example.test", "cdn.example.test", "api.github.com", "github.com");

    private readonly CancellationToken _cancellation;

    public TestBed(TestContext context, NetOptions? options = null)
    {
        _cancellation = context.CancellationToken;
        Start = Time.GetUtcNow();
        Server = new(Time);
        CacheRoot = Directory.CreateTempSubdirectory("autopilot-net-").FullName;
        Client = new(Server, options ?? new NetOptions("1.2.3"), Time, CacheRoot, Log);
    }

    /// <summary>One attempt only, so failure tests need no clock.</summary>
    public static NetOptions SingleAttempt => new("1.2.3") { RetryWaits = [] };

    /// <summary>Three attempts with no wait between them, for tests about what each attempt asks.</summary>
    public static NetOptions ImmediateRetries => new("1.2.3") { RetryWaits = [TimeSpan.Zero, TimeSpan.Zero] };

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));

    public DateTimeOffset Start { get; }

    public FakeServer Server { get; }

    /// <summary>What the client logged.</summary>
    public FakeLogger<NetClient> Log { get; } = new();

    /// <summary>The messages the client logged, in order.</summary>
    public string[] LogLines => [.. Log.Collector.GetSnapshot().Select(record => record.Message)];

    public string CacheRoot { get; }

    public NetClient Client { get; }

    /// <summary>When each request reached the server, in seconds since the test began.</summary>
    public double[] RequestSeconds => [.. Server.Requests.Select(request => (request.At - Start).TotalSeconds)];

    /// <summary>The <c>Range</c> header of each request, in order: <see langword="null"/> when it asked for the whole file.</summary>
    public string?[] RequestRanges => [.. Server.Requests.Select(request => request.Range?.ToString())];

    /// <summary>Moves the clock by each step in turn, so timers set along the way fire in order.</summary>
    public void Advance(params double[] seconds)
    {
        foreach (double step in seconds)
        {
            Time.Advance(TimeSpan.FromSeconds(step));
        }
    }

    public Task<TextAnswer> GetText(string url, TextRequest? options = null, HostAllowlist? hosts = null) =>
        Client.GetTextAsync(new(url), hosts ?? Hosts, options, _cancellation);

    public Task<TextAnswer> GetText(string url, CancellationToken cancellation) =>
        Client.GetTextAsync(new(url), Hosts, null, cancellation);

    public Task<NetException> GetTextFails(string url, TextRequest? options = null, HostAllowlist? hosts = null) =>
        Assert.ThrowsAsync<NetException>(() => GetText(url, options, hosts));

    public Task<HeadAnswer> Head(string url, HostAllowlist? hosts = null) =>
        Client.HeadAsync(new(url), hosts ?? Hosts, _cancellation);

    public Task<NetException> HeadFails(string url, HostAllowlist? hosts = null) =>
        Assert.ThrowsAsync<NetException>(() => Head(url, hosts));

    public void Dispose()
    {
        Client.Dispose();
        Directory.Delete(CacheRoot, recursive: true);
    }
}

internal sealed class ProgressLog : IProgress<DownloadProgress>
{
    public List<DownloadProgress> Reports { get; } = [];

    public void Report(DownloadProgress value) => Reports.Add(value);
}
