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

    /// <summary>
    /// What changed from this snapshot to <paramref name="after"/>; null when nothing did. The files of
    /// <paramref name="rewritten"/> (a record saved back whole) may carry a new modified time.
    /// </summary>
    public string? Difference(TreeSnapshot after, params IReadOnlyCollection<string> rewritten)
    {
        TreeChange change = Change(after, rewritten);
        List<string> parts = [];
        AddPart(parts, "added", change.Added);
        AddPart(parts, "removed", change.Removed);
        AddPart(parts, "changed", [.. change.Changed.Select(path => $"{path} ({_files[path].Change(after._files[path])})")]);
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    /// <summary>
    /// The paths added, removed and changed from this snapshot to <paramref name="after"/>, each sorted. The files of
    /// <paramref name="rewritten"/> may carry a new modified time.
    /// </summary>
    public TreeChange Change(TreeSnapshot after, params IReadOnlyCollection<string> rewritten)
    {
        ArgumentNullException.ThrowIfNull(after);
        HashSet<string> timeFree = [with(StringComparer.OrdinalIgnoreCase), .. rewritten];
        return new(
            [.. after._files.Keys.Where(path => !_files.ContainsKey(path)).Order(StringComparer.OrdinalIgnoreCase)],
            [.. _files.Keys.Where(path => !after._files.ContainsKey(path)).Order(StringComparer.OrdinalIgnoreCase)],
            [
                .. _files
                    .Where(file => after._files.TryGetValue(file.Key, out FileFacts? facts)
                        && (timeFree.Contains(file.Key) ? facts with { LastWriteUtc = file.Value.LastWriteUtc } : facts) != file.Value)
                    .Select(file => file.Key)
                    .Order(StringComparer.OrdinalIgnoreCase),
            ]);
    }

    /// <summary>The SHA-256 of the file at <paramref name="path"/>, hex; null when the tree has none there.</summary>
    public string? HashOf(string path) => _files.GetValueOrDefault(path)?.Sha256;

    /// <summary>
    /// Fails the test, listing the added, removed and changed paths, unless <paramref name="after"/> is the same
    /// tree; the files of <paramref name="rewritten"/> may carry a new modified time.
    /// </summary>
    public void AssertSame(TreeSnapshot after, params IReadOnlyCollection<string> rewritten)
    {
        if (Difference(after, rewritten) is { } difference)
        {
            Assert.Fail("the tree changed: " + difference);
        }
    }

    private static void AddPart(List<string> parts, string label, IReadOnlyList<string> paths)
    {
        if (paths.Count > 0)
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

/// <summary>What changed in a tree, by relative <c>/</c> separated path, each list sorted.</summary>
/// <param name="Added">The files that are new.</param>
/// <param name="Removed">The files gone.</param>
/// <param name="Changed">The files whose content, attributes or modified time changed.</param>
internal sealed record TreeChange(IReadOnlyList<string> Added, IReadOnlyList<string> Removed, IReadOnlyList<string> Changed);
