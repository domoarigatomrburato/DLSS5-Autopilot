namespace Autopilot.Core.Archives;

/// <summary>
/// A destination written as <c>&lt;destination&gt;.part</c> and moved into place only once every
/// check passed, so a refused or cancelled extraction leaves nothing.
/// </summary>
internal sealed class PartDestination
{
    private readonly string _destination;
    private readonly bool _isDirectory;

    private PartDestination(string destination, bool isDirectory)
    {
        _destination = destination;
        _isDirectory = isDirectory;
        Path = PathFor(destination);
    }

    public string Path { get; }

    public static string PathFor(string destination) => destination + ".part";

    /// <summary>Refuses an existing destination, removes a <c>.part</c> an earlier crash left, and makes room for the new one.</summary>
    public static PartDestination Prepare(string archivePath, string destination, bool isDirectory)
    {
        if (System.IO.Path.Exists(destination))
        {
            throw ArchiveException.For(ArchiveFailure.DestinationExists, archivePath, $"the destination {destination} already exists");
        }

        PartDestination part = new(destination, isDirectory);
        part.Discard();
        string? parent = System.IO.Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(parent))
        {
            _ = Directory.CreateDirectory(parent);
        }

        if (isDirectory)
        {
            _ = Directory.CreateDirectory(part.Path);
        }

        return part;
    }

    public void Commit()
    {
        if (_isDirectory)
        {
            Directory.Move(Path, _destination);
        }
        else
        {
            File.Move(Path, _destination);
        }
    }

    public void Discard()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
        else if (File.Exists(Path))
        {
            File.Delete(Path);
        }
    }
}
