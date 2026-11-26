using System.Formats.Tar;
using System.IO.Compression;

namespace Autopilot.Core.Archives;

/// <summary>
/// tar.gz, read with System.Formats.Tar over GZipStream. A tar has no index, so every check runs in
/// the one streaming pass, before each member's data is read; the ratio is taken over the whole
/// archive, against the size of the .tar.gz file.
/// </summary>
internal static class TarGzSource
{
    public static async Task ExtractAsync(string archivePath, MemberCatalog catalog, IExtractionTarget target, BoundedCopy copy, CancellationToken ct)
    {
        await using FileStream file = File.OpenRead(archivePath);
        long compressed = file.Length;
        await using GZipStream gzip = new(file, CompressionMode.Decompress);
        await using TarReader tar = new(gzip);

        while (await tar.GetNextEntryAsync(copyData: false, ct) is { } entry)
        {
            if (entry.EntryType is TarEntryType.GlobalExtendedAttributes)
            {
                // Metadata such as the commit id git archive writes; nothing to unpack.
                catalog.Count();
                continue;
            }

            string? path = Admit(archivePath, catalog, entry);
            catalog.CheckRatio(catalog.DeclaredTotal, compressed, "the archive");
            if (entry.EntryType is not TarEntryType.Directory && path is not null && target.Place(path) is { } destination)
            {
                await copy.WriteAsync(entry.DataStream ?? Stream.Null, destination, entry.Length, path, ct);
            }
        }
    }

    private static string? Admit(string archivePath, MemberCatalog catalog, TarEntry entry) => entry.EntryType switch
    {
        TarEntryType.Directory => catalog.Admit(entry.Name, isDirectory: true, 0),
        TarEntryType.RegularFile or TarEntryType.V7RegularFile or TarEntryType.ContiguousFile =>
            catalog.Admit(entry.Name, isDirectory: false, entry.Length),
        TarEntryType.SymbolicLink or TarEntryType.HardLink =>
            throw ArchiveException.For(ArchiveFailure.Link, archivePath, $"member '{entry.Name}' is a link to '{entry.LinkName}'"),
        _ => throw ArchiveException.For(ArchiveFailure.UnsupportedEntry, archivePath, $"member '{entry.Name}' is a {entry.EntryType} entry"),
    };
}
