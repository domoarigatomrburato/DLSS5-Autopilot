using Autopilot.Core.Resources;
using Autopilot.Core.Storage;

namespace Autopilot.Core.Library;

/// <summary>Whether the library cache was saved, and why not; a cache that is not saved only costs the next launch a scan.</summary>
public sealed record CacheSaveResult
{
    internal CacheSaveResult(Exception? failure) => Failure = failure;

    /// <summary>Why the cache was not saved, or null when it was.</summary>
    public Exception? Failure { get; }

    public bool Saved => Failure is null;

    /// <summary>Whether the save found its drive full.</summary>
    public bool DriveFull => Failure is not null && IoErrors.IsDiskFull(Failure);

    /// <summary>The line for the log, or null when the cache was saved.</summary>
    public string? Explain() => Failure switch
    {
        null => null,
        _ when DriveFull => Texts.Text("LibraryCacheDriveFull"),
        var failure => Texts.Format("LibraryCacheNotSaved", failure.Message),
    };
}
