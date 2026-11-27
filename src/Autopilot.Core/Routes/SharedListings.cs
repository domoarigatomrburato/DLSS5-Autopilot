using Autopilot.Core.Graphics;

namespace Autopilot.Core.Routes;

/// <summary>
/// One listing of each folder's subfolders for one read of a game: the runtime walk and the Remix
/// search cover overlapping folders, and each folder is listed once between them. A folder is
/// known by its path as spelled, so every answer keeps the spelling it would have had alone.
/// </summary>
internal sealed class SharedListings
{
    private readonly Dictionary<string, IReadOnlyList<string>> _subfolders = [with(StringComparer.Ordinal)];

    /// <summary>The subfolders of <paramref name="folder"/> that are not links, sorted by name, as <see cref="FolderListing.RealFolders"/> lists them.</summary>
    public IReadOnlyList<string> RealFolders(string folder, CancellationToken cancellationToken)
    {
        if (!_subfolders.TryGetValue(folder, out IReadOnlyList<string>? subfolders))
        {
            subfolders = FolderListing.RealFolders(folder, cancellationToken);
            _subfolders[folder] = subfolders;
        }

        return subfolders;
    }
}
