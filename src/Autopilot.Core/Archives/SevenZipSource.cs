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
    public static async Task ExtractAsync(string archivePath, MemberCatalog catalog, IExtractionTarget target, BoundedCopy copy, CancellationToken ct)
    {
        await using FileStream file = File.OpenRead(archivePath);
        await using IAsyncArchive archive = await SevenZipArchive.OpenAsyncArchive(file, ReaderOptions.ForExternalStream, ct);

        // Keyed by the raw name, so the second pass finds a member without checking its name again.
        Dictionary<string, (string Path, string Destination)> plan = [with(StringComparer.Ordinal)];
        await foreach (IArchiveEntry entry in archive.EntriesAsync.WithCancellation(ct))
        {
            if (catalog.Admit(entry.Key ?? "", entry.IsDirectory, entry.Size) is { } path
                && !entry.IsDirectory
                && target.Place(path) is { } destination)
            {
                plan[entry.Key ?? ""] = (path, destination);
            }
        }

        catalog.CheckRatio(catalog.DeclaredTotal, await archive.TotalSizeAsync(), "the archive");
        if (plan.Count > 0)
        {
            await WritePlannedAsync(archive, plan, copy, ct);
        }
    }

    private static async Task WritePlannedAsync(IAsyncArchive archive, Dictionary<string, (string Path, string Destination)> plan, BoundedCopy copy, CancellationToken ct)
    {
        await using IAsyncReader reader = await archive.ExtractAllEntriesAsync();
        int remaining = plan.Count;
        while (remaining > 0 && await reader.MoveToNextEntryAsync(ct))
        {
            IEntry entry = reader.Entry;
            if (!entry.IsDirectory && plan.TryGetValue(entry.Key ?? "", out (string Path, string Destination) planned))
            {
                await using Stream data = await reader.OpenEntryStreamAsync(ct);
                await copy.WriteAsync(data, planned.Destination, entry.Size, planned.Path, ct);
                remaining--;
            }
        }
    }
}
