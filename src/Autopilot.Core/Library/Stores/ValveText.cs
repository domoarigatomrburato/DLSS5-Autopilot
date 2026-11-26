using System.Globalization;
using ValveKeyValue;

namespace Autopilot.Core.Library.Stores;

/// <summary>Reads Valve's KeyValues text files (<c>.vdf</c>, <c>.acf</c>) as untrusted input.</summary>
internal static class ValveText
{
    private static readonly KVSerializer _serializer = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);

    // Steam writes paths with doubled backslashes and names with escaped quotes.
    private static readonly KVSerializerOptions _options = new() { HasEscapeSequences = true };

    /// <summary>The file's top object; null when it is missing, oversize, unreadable or malformed.</summary>
    public static async Task<KVObject?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        byte[]? bytes = await StoreFiles.ReadAsync(path, cancellationToken);
        if (bytes is null)
        {
            return null;
        }

        try
        {
            using MemoryStream stream = new(bytes, writable: false);
            KVObject root = _serializer.Deserialize(stream, _options).Root;
            return root.IsCollection ? root : null;
        }
        catch (Exception e) when (e is KeyValueException or InvalidDataException or FormatException or InvalidOperationException or ArgumentException or EndOfStreamException)
        {
            return null;
        }
    }

    /// <summary>A child of a collection by key, ignoring case as Steam does; null when absent.</summary>
    public static KVObject? Child(KVObject parent, string key) =>
        parent.Children.FirstOrDefault(child => string.Equals(child.Key, key, StringComparison.OrdinalIgnoreCase)).Value;

    /// <summary>A value as text; null when it is absent or a collection.</summary>
    public static string? Text(KVObject? value) =>
        value is null || value.IsCollection || value.IsArray || value.IsNull ? null : value.ToString(CultureInfo.InvariantCulture);

    /// <summary>A child's value as text; null when absent or a collection.</summary>
    public static string? Text(KVObject parent, string key) => Text(Child(parent, key));
}
