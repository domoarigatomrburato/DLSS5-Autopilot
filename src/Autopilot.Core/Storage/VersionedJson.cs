using System.Text.Json.Nodes;

namespace Autopilot.Core.Storage;

/// <summary>
/// Reads a JSON file the tool writes whole and versions (a store file, the install record) and says
/// what it found: missing, present, unreadable for now, damaged, or written by a newer version. The
/// file is untrusted input: capped in size and parsed strictly.
/// </summary>
internal static class VersionedJson
{
    /// <summary>
    /// The file at <paramref name="path"/> and its state. A file larger than <paramref name="maxBytes"/>,
    /// a folder, and a file that does not parse, holds a key twice, is not an object or holds a version
    /// under <paramref name="versionKey"/> that is not an integer are damaged, so none is ever rewritten
    /// silently. A version above <paramref name="knownVersion"/> is newer; an absent one is the known one.
    /// </summary>
    public static VersionedRead Read(string path, long maxBytes, string versionKey, int knownVersion)
    {
        FileBytes read = BoundedFile.Read(path, maxBytes);
        switch (read.State)
        {
            case FileBytesState.Missing:
                return new(JsonFileState.Missing, null);
            case FileBytesState.Unreadable:
                return new(JsonFileState.Unreadable, null);
        }

        if (read.Bytes is null || UntrustedJson.ParseObject(read.Bytes) is not { } document)
        {
            return new(JsonFileState.Damaged, null);
        }

        if (!document.TryGetPropertyValue(versionKey, out JsonNode? stamped))
        {
            return new(JsonFileState.Present, document);
        }

        if (stamped is not JsonValue value || !value.TryGetValue(out int version))
        {
            return new(JsonFileState.Damaged, null);
        }

        return new(version > knownVersion ? JsonFileState.Newer : JsonFileState.Present, document);
    }
}

/// <summary>What <see cref="VersionedJson.Read"/> found; <see cref="Document"/> is set for <see cref="JsonFileState.Present"/> and <see cref="JsonFileState.Newer"/>.</summary>
internal sealed record VersionedRead(JsonFileState State, JsonObject? Document);

/// <summary>The state of a versioned JSON file.</summary>
internal enum JsonFileState
{
    /// <summary>There is no file: empty defaults.</summary>
    Missing,

    /// <summary>A version this build knows, parsed.</summary>
    Present,

    /// <summary>There, but it could not be read now (locked by another program, denied).</summary>
    Unreadable,

    /// <summary>Too large, a folder, not JSON, a key twice, not an object, or a version that is not an integer.</summary>
    Damaged,

    /// <summary>Written by a newer version: read where it can be, never written.</summary>
    Newer,
}
