using System.IO.Compression;

namespace Autopilot.Core.Archives;

/// <summary>Zip, read with System.IO.Compression: every header is checked before anything is written.</summary>
internal static class ZipSource
{
    /// <summary>Reads the zip that starts at <paramref name="start"/> in the file: past the program it is appended to, or 0.</summary>
    public static async Task ExtractAsync(string archivePath, long start, MemberCatalog catalog, IExtractionTarget target, BoundedCopy copy, CancellationToken ct)
    {
        await using Stream zipBytes = Open(archivePath, start);
        await using ZipArchive zip = await ZipArchive.CreateAsync(zipBytes, ZipArchiveMode.Read, leaveOpen: true, entryNameEncoding: null, ct);

        List<(ZipArchiveEntry Entry, string Path, string Target)> plan = [];
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            bool isDirectory = entry.FullName.EndsWith('/', StringComparison.Ordinal) || entry.FullName.EndsWith('\\', StringComparison.Ordinal);
            string? path = catalog.Admit(entry.FullName, isDirectory, entry.Length);
            if (isDirectory || path is null)
            {
                continue;
            }

            catalog.CheckRatio(entry.Length, entry.CompressedLength, path);
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

    // The file, or the view of it from the zip's start; the file is closed if the view cannot be made.
    private static Stream Open(string archivePath, long start)
    {
        FileStream file = File.OpenRead(archivePath);
        if (start == 0)
        {
            return file;
        }

        try
        {
            return new OffsetStream(file, start);
        }
        catch
        {
            file.Dispose();
            throw;
        }
    }
}
