using System.Text.Json;
using System.Text.Json.Serialization;
using Autopilot.Core.Routes;

namespace Autopilot.Core.Install;

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
