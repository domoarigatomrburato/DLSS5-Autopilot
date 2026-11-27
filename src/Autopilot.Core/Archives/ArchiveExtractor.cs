using SharpCompress.Common;

namespace Autopilot.Core.Archives;

/// <summary>
/// Unpacks zip, tar.gz and 7z archives without starting a process. Every member is checked before
/// anything is written, the result lands in a <c>.part</c> file or folder, and it is moved into
/// place only once every check passed.
/// </summary>
public static class ArchiveExtractor
{
    /// <summary>
    /// Writes the one file member whose path ends with <paramref name="memberEnding"/> (whole path
    /// parts, ignoring case) to <paramref name="destinationFile"/>, which must not exist.
    /// </summary>
    /// <returns><paramref name="destinationFile"/>, as given.</returns>
    /// <exception cref="ArchiveException">The archive was refused; nothing was written.</exception>
    public static async Task<string> ExtractMemberAsync(
        string archivePath,
        string memberEnding,
        string destinationFile,
        ArchiveLimits? limits,
        CancellationToken ct)
    {
        // The caller's arguments are checked before the .part exists, so a bad one leaves nothing.
        MemberTarget target = new(archivePath, memberEnding, PartDestination.PathFor(destinationFile));
        var part = PartDestination.Prepare(archivePath, destinationFile, isDirectory: false);
        await ExtractAsync(archivePath, part, target, limits, ct);
        return destinationFile;
    }

    /// <summary>
    /// Writes the file members <paramref name="options"/> selects, with their subfolders, into
    /// <paramref name="destinationDirectory"/>, which must not exist.
    /// </summary>
    /// <returns>The files written, in the caller's spelling of the destination.</returns>
    /// <exception cref="ArchiveException">The archive was refused; nothing was written.</exception>
    public static async Task<IReadOnlyList<string>> ExtractTreeAsync(
        string archivePath,
        string destinationDirectory,
        TreeOptions? options,
        ArchiveLimits? limits,
        CancellationToken ct)
    {
        TreeTarget target = new(archivePath, destinationDirectory, PartDestination.PathFor(destinationDirectory), options ?? new TreeOptions());
        var part = PartDestination.Prepare(archivePath, destinationDirectory, isDirectory: true);
        await ExtractAsync(archivePath, part, target, limits, ct);
        return target.Written;
    }

    /// <summary>
    /// The file members of a zip (plain or appended to a program) or a tar.gz, in archive order,
    /// after every check extraction runs on the headers (names, duplicates, links, entry count,
    /// total size, ratio). Nothing is written, not even a <c>.part</c>; a tar.gz is read in one
    /// streaming pass that skips its data.
    /// </summary>
    /// <exception cref="ArchiveException">
    /// The archive was refused, or is a 7z (<see cref="ArchiveFailure.NotListable"/>), whose members cannot be listed without unpacking it.
    /// </exception>
    public static async Task<IReadOnlyList<ArchiveMember>> ListAsync(string archivePath, ArchiveLimits? limits, CancellationToken ct)
    {
        MemberCatalog catalog = await ReadAsync(archivePath, new ListingTarget(), limits, ct);
        return catalog.Files;
    }

    private static async Task ExtractAsync(
        string archivePath,
        PartDestination part,
        IExtractionTarget target,
        ArchiveLimits? limits,
        CancellationToken ct)
    {
        try
        {
            _ = await ReadAsync(archivePath, target, limits, ct);
            part.Commit();
        }
        catch
        {
            part.Discard();
            throw;
        }
    }

    // Opens the archive by its format and hands each member to the target, checking every one on
    // the catalog it returns. A listing writes nothing, and its catalog keeps the files it admitted.
    private static async Task<MemberCatalog> ReadAsync(string archivePath, IExtractionTarget target, ArchiveLimits? limits, CancellationToken ct)
    {
        bool listing = target is ListingTarget;
        MemberCatalog catalog = new(archivePath, limits ?? ArchiveLimits.Default, keepFiles: listing);
        using BoundedCopy copy = new(archivePath);
        try
        {
            ct.ThrowIfCancellationRequested();
            DetectedArchive archive = await ArchiveSignature.DetectAsync(archivePath, ct);
            switch (archive.Format)
            {
                case ArchiveFormat.Zip:
                    await ZipSource.ExtractAsync(archivePath, archive.Start, catalog, target, copy, ct);
                    break;
                case ArchiveFormat.TarGz:
                    await TarGzSource.ExtractAsync(archivePath, catalog, target, copy, ct);
                    break;
                case ArchiveFormat.SevenZip when listing:
                    throw ArchiveException.For(ArchiveFailure.NotListable, archivePath, "is a 7z archive, whose members are not listed without unpacking it");
                default:
                    await SevenZipSource.ExtractAsync(archivePath, catalog, target, copy, ct);
                    break;
            }
            target.Finish();
            return catalog;
        }
        catch (Exception damaged) when (IsDamage(damaged))
        {
            throw ArchiveException.For(ArchiveFailure.Corrupt, archivePath, $"is damaged or cut short ({damaged.Message})", damaged);
        }
    }

    // Format errors the readers raise for a damaged or truncated archive. Other I/O errors, disk
    // full included, propagate unchanged.
    private static bool IsDamage(Exception exception) =>
        exception is InvalidDataException or EndOfStreamException or SharpCompressException;
}
