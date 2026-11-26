using System.IO.Compression;

namespace Autopilot.Core.Archives;

/// <summary>Zip, read with System.IO.Compression: every header is checked before anything is written.</summary>
internal static class ZipSource
{
    public static async Task ExtractAsync(string archivePath, MemberCatalog catalog, IExtractionTarget target, BoundedCopy copy, CancellationToken ct)
    {
        await using FileStream file = File.OpenRead(archivePath);
        await using ZipArchive zip = await ZipArchive.CreateAsync(file, ZipArchiveMode.Read, leaveOpen: false, entryNameEncoding: null, ct);

        List<(ZipArchiveEntry Entry, string Path, string Target)> plan = [];
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            bool isDirectory = entry.FullName.EndsWith('/', StringComparison.Ordinal) || entry.FullName.EndsWith('\\', StringComparison.Ordinal);
            string? path = catalog.Admit(entry.FullName, isDirectory, entry.Length);
            if (isDirectory || path is null)
            {
                continue;
            }

            catalog.CheckRatio(entry.Length, entry.CompressedLength, $"member '{path}'");
            if (target.Place(path) is { } destination)
            {
                plan.Add((entry, path, destination));
            }
        }

        foreach ((ZipArchiveEntry entry, string path, string destination) in plan)
        {
            await using Stream data = await entry.OpenAsync(ct);
            await copy.WriteAsync(data, destination, entry.Length, path, ct);
        }
    }
}
