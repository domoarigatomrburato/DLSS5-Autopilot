using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Autopilot.Core.Net;

/// <summary>API answers kept under <c>&lt;cache&gt;/api/&lt;key&gt;.json</c>, written atomically.</summary>
internal sealed class ApiCache(string cacheRoot)
{
    private static readonly JsonSerializerOptions _json = new()
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    private readonly string _folder = Path.Combine(cacheRoot, "api");

    /// <summary>The kept answer for a URL; a missing or unreadable file is no answer.</summary>
    public async Task<CachedText?> ReadAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            await using FileStream file = File.OpenRead(PathFor(url));
            CachedText? entry = await JsonSerializer.DeserializeAsync<CachedText>(file, _json, cancellationToken);
            return entry?.Url == url.AbsoluteUri ? entry : null;
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Writes to a temporary file in the same folder, flushes it to disk, then moves it into place.</summary>
    public void Write(Uri url, CachedText entry)
    {
        _ = Directory.CreateDirectory(_folder);
        string temporary = Path.Combine(_folder, Path.GetRandomFileName() + ".tmp");
        try
        {
            using (FileStream file = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(file, entry, _json);
                file.Flush(flushToDisk: true);
            }

            File.Move(temporary, PathFor(url), overwrite: true);
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
    }

    private string PathFor(Uri url)
    {
        string key = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(url.AbsoluteUri)))[..32];
        return Path.Combine(_folder, key + ".json");
    }
}

internal sealed record CachedText(string Url, DateTimeOffset FetchedAt, string Body);
