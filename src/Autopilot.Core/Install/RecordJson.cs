using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Autopilot.Core.Routes;

namespace Autopilot.Core.Install;

/// <summary>
/// The install record and the failure note as JSON: camelCase keys, enums as kebab-case names (numbers
/// refused), the route by its stable id, every constructor parameter required and every non-nullable
/// value checked. Unknown keys are ignored.
/// </summary>
internal static class RecordJson
{
    // Built once and shared; the route's converter comes first, as the first converter that fits wins.
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        Converters =
        {
            new RouteIdJson(),
            // A number from a hand-edited or damaged file could be a value no code expects.
            new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower, allowIntegerValues: false),
        },
    };

    /// <summary>The record's shape.</summary>
    public static JsonTypeInfo<InstallRecord> Record { get; } = _options.GetTypeInfo<InstallRecord>();

    /// <summary>The failure note's shape.</summary>
    public static JsonTypeInfo<FailureNote> Note { get; } = _options.GetTypeInfo<FailureNote>();
}

/// <summary>A route by the stable id <see cref="RouteIds"/> keeps (<c>native</c>, <c>optiscaler</c> ...); anything else is refused.</summary>
internal sealed class RouteIdJson : JsonConverter<RouteId>
{
    public override RouteId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String && RouteIds.TryParse(reader.GetString(), out RouteId route)
            ? route
            : throw new JsonException("not a route id");

    public override void Write(Utf8JsonWriter writer, RouteId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Id());
}
