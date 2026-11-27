using System.Text;

namespace Autopilot.Core.Tests.TestBeds;

/// <summary>
/// One file of a game folder as a test describes it: a relative path (either slash), its content,
/// a size (the content padded with a sparse hole up to it), whether it is read-only, and when it was
/// last modified.
/// </summary>
internal sealed record TreeFile(string Path)
{
    public byte[]? Content { get; init; }

    /// <summary>The file's length when larger than its <see cref="Content"/>, padded with a sparse hole.</summary>
    public long Size { get; init; }

    public bool ReadOnly { get; init; }

    /// <summary>The last write time to set; unset, the time of writing.</summary>
    public DateTimeOffset? Modified { get; init; }

    public static TreeFile Bytes(string path, byte[] content) => new(path) { Content = content };

    public static TreeFile Text(string path, string text) => new(path) { Content = Encoding.UTF8.GetBytes(text) };

    public static TreeFile Sized(string path, long size) => new(path) { Size = size };
}

/// <summary>
/// A game folder in a temp directory, written from <see cref="TreeFile"/> descriptions; disposing
/// deletes it, read-only files and folders a test made unreadable included. Paths take either slash,
/// so a test spells them as Windows does.
/// </summary>
internal sealed class GameTree : IDisposable
{
    private readonly string _parent = Directory.CreateTempSubdirectory("autopilot-tree-").FullName;

    /// <param name="name">The game folder's own name, inside its temp parent.</param>
    public GameTree(string name = "Game")
    {
        Root = Path.Join(_parent, name);
        _ = Directory.CreateDirectory(Root);
    }

    /// <summary>The game folder.</summary>
    public string Root { get; }

    /// <summary>A tree holding <paramref name="files"/>.</summary>
    public static GameTree Of(params IEnumerable<TreeFile> files) => new GameTree().Add(files);

    /// <summary>The full path of <paramref name="relative"/>, spelled with either slash.</summary>
    public string PathOf(string relative) =>
        Path.Join(Root, relative.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Writes each file, creating its folders.</summary>
    public GameTree Add(params IEnumerable<TreeFile> files)
    {
        foreach (TreeFile file in files)
        {
            Write(file);
        }

        return this;
    }

    /// <summary>An empty folder.</summary>
    public GameTree Folder(string relative)
    {
        _ = Directory.CreateDirectory(PathOf(relative));
        return this;
    }

    public void Dispose()
    {
        Unlock(_parent);
        Directory.Delete(_parent, recursive: true);
    }

    // A test may leave files read-only and, off Windows, folders without access: deleting needs both back.
    private static void Unlock(string folder)
    {
        foreach (string file in Directory.EnumerateFiles(folder))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        foreach (string directory in Directory.EnumerateDirectories(folder))
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            if (!new DirectoryInfo(directory).Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                Unlock(directory);
            }
        }
    }

    private void Write(TreeFile file)
    {
        string path = PathOf(file.Path);
        TestFiles.Plant(path, file.Content ?? []);
        if (file.Size > (file.Content?.Length ?? 0))
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Write);
            stream.SetLength(file.Size);
        }

        if (file.Modified is { } modified)
        {
            File.SetLastWriteTimeUtc(path, modified.UtcDateTime);
        }

        if (file.ReadOnly)
        {
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
        }
    }
}
