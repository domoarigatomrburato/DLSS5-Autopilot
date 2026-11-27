using System.Text.Json;
using System.Text.Json.Serialization;
using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Storage;

namespace Autopilot.Core.Library;

/// <summary>
/// <c>library.json</c> as JSON: the web defaults (camelCase keys, either case read), enums by their
/// names as declared (numbers refused), every constructor parameter required and every non-nullable
/// value checked. Every enum the file reaches is listed in the converters.
/// </summary>
[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    Converters =
    [
        typeof(NamedOnly<GameSource>),
        typeof(NamedOnly<Bitness>),
        typeof(NamedOnly<GraphicsApi>),
        typeof(NamedOnly<ApiReason>),
        typeof(NamedOnly<GameReadFailure>),
        typeof(NamedOnly<InstallState>),
    ])]
[JsonSerializable(typeof(CacheFile))]
internal sealed partial class LibraryJsonContext : JsonSerializerContext;
