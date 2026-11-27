using System.Security.Cryptography;

namespace Autopilot.Core.Tests.TestBeds;

/// <summary>
/// Every file of a tree: its content's SHA-256, its attributes and its last write time, by relative
/// path ignoring case (as Windows compares them), so a round trip can be shown to leave the tree as it was.
/// </summary>
internal sealed class TreeSnapshot
{
    private readonly Dictionary<string, FileFacts> _files;

    private TreeSnapshot(Dictionary<string, FileFacts> files) => _files = files;

    /// <summary>The files' relative paths, with <c>/</c> separators.</summary>
    public IReadOnlyCollection<string> Paths => _files.Keys;

    /// <summary>Reads every file below <paramref name="root"/>.</summary>
    public static TreeSnapshot Of(string root)
    {
        Dictionary<string, FileFacts> files = [with(StringComparer.OrdinalIgnoreCase)];
        foreach (string path in Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 }))
        {
            using FileStream content = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            files[Path.GetRelativePath(root, path).Replace('\\', '/')] = new(
                Convert.ToHexString(SHA256.HashData(content)),
                File.GetAttributes(path),
                File.GetLastWriteTimeUtc(path));
        }

        return new(files);
    }

    /// <summary>What changed from this snapshot to <paramref name="after"/>; null when nothing did.</summary>
    public string? Difference(TreeSnapshot after)
    {
        ArgumentNullException.ThrowIfNull(after);
        string[] added = [.. after._files.Keys.Where(path => !_files.ContainsKey(path)).Order(StringComparer.OrdinalIgnoreCase)];
        string[] removed = [.. _files.Keys.Where(path => !after._files.ContainsKey(path)).Order(StringComparer.OrdinalIgnoreCase)];
        string[] changed =
        [
            .. _files
                .Where(file => after._files.TryGetValue(file.Key, out FileFacts? facts) && facts != file.Value)
                .Select(file => $"{file.Key} ({file.Value.Change(after._files[file.Key])})")
                .Order(StringComparer.OrdinalIgnoreCase),
        ];
        List<string> parts = [];
        AddPart(parts, "added", added);
        AddPart(parts, "removed", removed);
        AddPart(parts, "changed", changed);
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    /// <summary>Fails the test, listing the added, removed and changed paths, unless <paramref name="after"/> is the same tree.</summary>
    public void AssertSame(TreeSnapshot after)
    {
        if (Difference(after) is { } difference)
        {
            Assert.Fail("the tree changed: " + difference);
        }
    }

    private static void AddPart(List<string> parts, string label, string[] paths)
    {
        if (paths.Length > 0)
        {
            parts.Add($"{label}: {string.Join(", ", paths)}");
        }
    }

    private sealed record FileFacts(string Sha256, FileAttributes Attributes, DateTime LastWriteUtc)
    {
        public string Change(FileFacts after) => string.Join(", ", new[]
        {
            Sha256 != after.Sha256 ? "content" : null,
            Attributes != after.Attributes ? $"attributes {Attributes} -> {after.Attributes}" : null,
            LastWriteUtc != after.LastWriteUtc ? "modified time" : null,
        }.OfType<string>());
    }
}
