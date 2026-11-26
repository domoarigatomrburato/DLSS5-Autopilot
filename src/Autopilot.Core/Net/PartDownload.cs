namespace Autopilot.Core.Net;

/// <summary>
/// Fetches a download into <c>&lt;entry&gt;.part</c>, checks it, and moves it into place. Any failure,
/// refusal or cancellation removes the <c>.part</c>.
/// </summary>
internal sealed class PartDownload(NetClient client, Verifier verifier)
{
    public static string PartOf(string entry) => entry + ".part";

    public async Task<CachedFile> FetchAsync(Download download, FileRequest? options, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        string part = PartOf(download.Entry);
        File.Delete(part);
        try
        {
            long size = await client.StreamToAsync(download.Url, download.Hosts, PartStream.Opener(part), options, progress, cancellationToken);
            await verifier.CheckAsync(part, download.FileName, download.Url.Host, download.Expected, cancellationToken);
            PartStream.FlushToDisk(part);
            File.Move(part, download.Entry);
            return new(download.Entry, size, download.Expected);
        }
        catch
        {
            File.Delete(part);
            throw;
        }
    }
}

internal sealed record Download(Uri Url, HostAllowlist Hosts, string FileName, string Entry, Verification Expected);
