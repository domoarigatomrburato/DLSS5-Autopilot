namespace Autopilot.Core;

internal static class ExceptionChain
{
    /// <summary>
    /// The exception and everything it wraps, depth first: its inner exception, or every one an
    /// <see cref="AggregateException"/> holds.
    /// </summary>
    public static IEnumerable<Exception> Of(Exception exception)
    {
        Stack<Exception> pending = new([exception]);
        while (pending.TryPop(out Exception? current))
        {
            yield return current;
            if (current is AggregateException aggregate)
            {
                for (int i = aggregate.InnerExceptions.Count - 1; i >= 0; i--)
                {
                    pending.Push(aggregate.InnerExceptions[i]);
                }
            }
            else if (current.InnerException is { } inner)
            {
                pending.Push(inner);
            }
        }
    }
}
