using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Autopilot.Core.Tests.TestBeds;

/// <summary>
/// The logger factory a <see cref="Core.Components.ComponentRun"/> takes, handing out one
/// <see cref="FakeLogger"/> per category, the same across runs, so a test reads what one source logged.
/// </summary>
internal sealed class RunLoggers : ILoggerFactory
{
    private readonly ConcurrentDictionary<string, FakeLogger> _loggers = new(StringComparer.Ordinal);

    /// <summary>The logger <typeparamref name="T"/>'s category gets.</summary>
    public FakeLogger Of<T>() => Category(typeof(T).FullName!);

    public ILogger CreateLogger(string categoryName) => Category(categoryName);

    private FakeLogger Category(string name) => _loggers.GetOrAdd(name, category => new FakeLogger(new FakeLogCollector(), category));

    public void AddProvider(ILoggerProvider provider) => throw new NotSupportedException("the tests read each category's fake logger");

    public void Dispose()
    {
    }
}
