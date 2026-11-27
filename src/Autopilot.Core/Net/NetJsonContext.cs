using System.Text.Json.Serialization;

namespace Autopilot.Core.Net;

/// <summary>The kept API answers as JSON: every constructor parameter required and every non-nullable value checked.</summary>
[JsonSourceGenerationOptions(RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(CachedText))]
internal sealed partial class NetJsonContext : JsonSerializerContext;
