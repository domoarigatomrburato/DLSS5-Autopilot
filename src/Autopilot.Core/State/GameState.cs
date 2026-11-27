using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace Autopilot.Core.State;

/// <summary>
/// What the tool learned about one game, as a JSON object read per key. A value of the wrong type
/// (a hand-typed string where a number is expected) reads as its default and never throws; keys this
/// version does not know are kept when the state is written back.
/// </summary>
public sealed class GameState
{
    /// <summary>The schema version every file carries; the store writes it.</summary>
    public const string SchemaKey = "schema";

    /// <summary>The game's normalised install folder, which every file names; the store writes it.</summary>
    public const string FolderKey = "folder";

    private readonly JsonObject _document;

    internal GameState(JsonObject document) => _document = document;

    /// <summary>The value under <paramref name="key"/>; the default when it is absent or of another type.</summary>
    public T? Get<T>(string key, JsonTypeInfo<T> info)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (!_document.TryGetPropertyValue(key, out JsonNode? node) || node is null)
        {
            return default;
        }

        try
        {
            return node.Deserialize(info);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or NotSupportedException)
        {
            return default;
        }
    }

    /// <summary>Sets <paramref name="key"/> to <paramref name="value"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is one the store writes itself.</exception>
    public void Set<T>(string key, T value, JsonTypeInfo<T> info)
    {
        Writable(key);
        _document[key] = JsonSerializer.SerializeToNode(value, info);
    }

    /// <summary>Removes <paramref name="key"/>, when present.</summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is one the store writes itself.</exception>
    public void Remove(string key)
    {
        Writable(key);
        _ = _document.Remove(key);
    }

    private static void Writable(string key)
    {
        if (key is SchemaKey or FolderKey)
        {
            throw new ArgumentException($"the store writes {key} itself", nameof(key));
        }
    }
}
