using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Autopilot.Core.Archives;

/// <summary>
/// 7z, read with SharpCompress. Its headers are checked before anything is written; members are
/// then read in order, so a solid block is decompressed once. SharpCompress reports no packed
/// size per member or per block, only the archive's, so the ratio is taken over the whole archive.
/// </summary>
internal static class SevenZipSource
{
    public static async Task ExtractAsync(string archivePath, MemberCatalog catalog, IExtractionTarget target, CancellationToken ct)
    {
        await using FileStream file = File.OpenRead(archivePath);
        await using IAsyncArchive archive = await SevenZipArchive.OpenAsyncArchive(file, ReaderOptions.ForExternalStream, ct);

        Dictionary<string, string> plan = [with(StringComparer.OrdinalIgnoreCase)];
        await foreach (IArchiveEntry entry in archive.EntriesAsync.WithCancellation(ct))
        {
            if (catalog.Admit(entry.Key ?? "", entry.IsDirectory, entry.Size) is { } path
                && !entry.IsDirectory
                && target.Place(path) is { } destination)
            {
                plan[path] = destination;
            }
        }

        catalog.CheckRatio(catalog.DeclaredTotal, await archive.TotalSizeAsync(), "the archive");
        if (plan.Count > 0)
        {
            await WritePlannedAsync(archivePath, archive, plan, ct);
        }
    }

    private static async Task WritePlannedAsync(string archivePath, IAsyncArchive archive, Dictionary<string, string> plan, CancellationToken ct)
    {
        await using IAsyncReader reader = await archive.ExtractAllEntriesAsync();
        int remaining = plan.Count;
        while (remaining > 0 && await reader.MoveToNextEntryAsync(ct))
        {
            IEntry entry = reader.Entry;
            string path = string.Join('/', SafePath.Parts(entry.Key ?? ""));
            if (!entry.IsDirectory && plan.TryGetValue(path, out string? destination))
            {
                await using Stream data = await reader.OpenEntryStreamAsync(ct);
                await BoundedCopy.WriteAsync(data, destination, entry.Size, archivePath, path, ct);
                remaining--;
            }
        }
    }
}
