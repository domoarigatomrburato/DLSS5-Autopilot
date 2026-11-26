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
        ArchiveLimits? limits = null,
        CancellationToken ct = default)
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
        TreeOptions? options = null,
        ArchiveLimits? limits = null,
        CancellationToken ct = default)
    {
        TreeTarget target = new(archivePath, destinationDirectory, PartDestination.PathFor(destinationDirectory), options ?? new TreeOptions());
        var part = PartDestination.Prepare(archivePath, destinationDirectory, isDirectory: true);
        await ExtractAsync(archivePath, part, target, limits, ct);
        return target.Written;
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
            ct.ThrowIfCancellationRequested();
            MemberCatalog catalog = new(archivePath, limits ?? ArchiveLimits.Default);
            Task reading = await ArchiveSignature.DetectAsync(archivePath, ct) switch
            {
                ArchiveFormat.Zip => ZipSource.ExtractAsync(archivePath, catalog, target, ct),
                ArchiveFormat.TarGz => TarGzSource.ExtractAsync(archivePath, catalog, target, ct),
                _ => SevenZipSource.ExtractAsync(archivePath, catalog, target, ct),
            };
            await reading;
            target.Finish();
            part.Commit();
        }
        catch (Exception damaged) when (IsDamage(damaged))
        {
            part.Discard();
            throw ArchiveException.For(ArchiveFailure.Corrupt, archivePath, $"is damaged or cut short ({damaged.Message})", damaged);
        }
        catch
        {
            part.Discard();
            throw;
        }
    }

    // Format errors the readers raise for a damaged or truncated archive. Other I/O errors, disk
    // full included, propagate unchanged.
    private static bool IsDamage(Exception exception) =>
        exception is InvalidDataException or EndOfStreamException or SharpCompressException;
}
