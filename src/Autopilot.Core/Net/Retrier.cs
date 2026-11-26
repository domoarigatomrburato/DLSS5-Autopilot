using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Net;

/// <summary>
/// Runs attempts until one succeeds, a failure is not worth another, or the attempts run out. The
/// overall limit spans every attempt and every wait between them; a stall drops only one attempt.
/// </summary>
internal sealed partial class Retrier(NetOptions options, TimeProvider time, ILogger logger)
{
    public async Task<T> RunAsync<T>(Uri url, TimeSpan timeout, Func<Attempt, Task<T>> run, CancellationToken cancellationToken)
    {
        using CancellationTokenSource overall = new(timeout, time);
        using var call = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, overall.Token);
        string path = PathOnly(url);
        try
        {
            for (int number = 0; ; number++)
            {
                try
                {
                    return await RunAttemptAsync(url, run, overall, call.Token, cancellationToken);
                }
                catch (NetException failure) when (failure.IsRetryable && number < options.RetryWaits.Count)
                {
                    TimeSpan wait = failure.RetryAfter ?? options.RetryWaits[number];
                    if (logger.IsEnabled(LogLevel.Information))
                    {
                        LogRetry(logger, number + 1, path, failure.Host, failure.Message, wait);
                    }

                    await WaitAsync(wait, url, overall, call.Token, cancellationToken);
                }
            }
        }
        catch (NetException failure)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                LogFailure(logger, path, failure.Host, failure.Kind);
            }

            throw;
        }
    }

    private async Task<T> RunAttemptAsync<T>(
        Uri url,
        Func<Attempt, Task<T>> run,
        CancellationTokenSource overall,
        CancellationToken call,
        CancellationToken cancellationToken)
    {
        using Attempt attempt = new(url, time, options.StallTimeout, call);
        try
        {
            return await run(attempt);
        }
        catch (OperationCanceledException cancelled) when (!cancellationToken.IsCancellationRequested && (overall.IsCancellationRequested || attempt.Stalled))
        {
            bool stalled = !overall.IsCancellationRequested;
            string what = stalled ? $"no bytes for {options.StallTimeout}" : "over the overall time limit";
            throw new NetException(NetErrorKind.Timeout, attempt.Url.Host, $"{attempt.Url.Host}: {what}", cancelled) { IsRetryable = stalled };
        }
        catch (OperationCanceledException cancelled) when (cancellationToken.IsCancellationRequested && cancelled.CancellationToken != cancellationToken)
        {
            throw new OperationCanceledException(cancelled.Message, cancelled, cancellationToken);
        }
        catch (HttpRequestException failure)
        {
            throw NetFailures.FromTransport(failure, attempt.Url.Host);
        }
    }

    private async Task WaitAsync(TimeSpan wait, Uri url, CancellationTokenSource overall, CancellationToken call, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(wait, time, call);
        }
        catch (OperationCanceledException cancelled) when (!cancellationToken.IsCancellationRequested && overall.IsCancellationRequested)
        {
            throw new NetException(NetErrorKind.Timeout, url.Host, $"{url.Host}: over the overall time limit", cancelled);
        }
        catch (OperationCanceledException cancelled) when (cancellationToken.IsCancellationRequested && cancelled.CancellationToken != cancellationToken)
        {
            throw new OperationCanceledException(cancelled.Message, cancelled, cancellationToken);
        }
    }

    // GitHub's asset redirects carry signed tokens in the query, so a URL is logged without it.
    private static string PathOnly(Uri url) => url.GetLeftPart(UriPartial.Path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Attempt {Attempt} at {Url} failed at {Host} ({Reason}); trying again in {Wait}")]
    private static partial void LogRetry(ILogger logger, int attempt, string url, string host, string reason, TimeSpan wait);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Url} failed at {Host}: {Kind}")]
    private static partial void LogFailure(ILogger logger, string url, string host, NetErrorKind kind);
}
