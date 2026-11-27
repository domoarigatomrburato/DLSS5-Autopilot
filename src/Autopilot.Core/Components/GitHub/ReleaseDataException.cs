namespace Autopilot.Core.Components.GitHub;

/// <summary>
/// GitHub's API answered something that is not release data. Neither an older answer nor
/// github.com's pages stand in for it: the API was reached, and what it said is the problem.
/// </summary>
public sealed class ReleaseDataException(Uri url, string message, Exception? innerException = null) : Exception(message, innerException)
{
    /// <summary>The API address that was asked.</summary>
    public Uri Url { get; } = url;
}
