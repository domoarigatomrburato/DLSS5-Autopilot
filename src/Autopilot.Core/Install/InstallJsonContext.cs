using System.Text.Json.Serialization;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.Sources;
using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Net;
using Autopilot.Core.Routes;
using Autopilot.Core.Storage;

namespace Autopilot.Core.Install;

/// <summary>
/// The install record and the failure note as JSON: camelCase keys, enums as kebab-case names (numbers
/// refused), the route by its stable id, every constructor parameter required and every non-nullable
/// value checked. Unknown keys are ignored.
/// </summary>
/// <remarks>Every enum the two reach is listed in the converters; the route's converter comes first, as the first converter that fits wins.</remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    WriteIndented = true,
    Converters =
    [
        typeof(RouteIdJson),
        typeof(KebabNamedOnly<RecordState>),
        typeof(KebabNamedOnly<Bitness>),
        typeof(KebabNamedOnly<GraphicsApi>),
        typeof(KebabNamedOnly<GameSource>),
        typeof(KebabNamedOnly<EntryClass>),
        typeof(KebabNamedOnly<EntryState>),
        typeof(KebabNamedOnly<FileAttributes>),
        typeof(KebabNamedOnly<BackupKind>),
        typeof(KebabNamedOnly<RegisteredLayer>),
        typeof(KebabNamedOnly<FailureOperation>),
        typeof(KebabNamedOnly<FailureKind>),
        typeof(KebabNamedOnly<FailureOutcome>),
        typeof(KebabNamedOnly<NetErrorKind>),
        typeof(KebabNamedOnly<DriveRoles>),
        typeof(KebabNamedOnly<ReShadeProxy>),
        typeof(KebabNamedOnly<OptiScalerProxy>),
        typeof(KebabNamedOnly<OptiScalerBuildKey>),
        typeof(KebabNamedOnly<NeuralPreset>),
        typeof(KebabNamedOnly<NeuralStyle>),
        typeof(KebabNamedOnly<NeuralPasses>),
        typeof(KebabNamedOnly<OwnFrameGeneration>),
        typeof(KebabNamedOnly<MotionProvider>),
        typeof(KebabNamedOnly<DlssPreset>),
        typeof(KebabNamedOnly<FeederHdr>),
        typeof(KebabNamedOnly<FeederPick>),
    ])]
[JsonSerializable(typeof(InstallRecord))]
[JsonSerializable(typeof(FailureNote))]
internal sealed partial class InstallJsonContext : JsonSerializerContext;
