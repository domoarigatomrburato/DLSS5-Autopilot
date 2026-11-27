using System.Text.Json;
using System.Text.Json.Serialization;

namespace Autopilot.Core.Storage;

/// <summary>
/// Enums by name only, spelled as declared: a number from a hand-edited or damaged file could be a
/// value no code expects. Listed per enum in a source-generated context's converters.
/// </summary>
internal class NamedOnly<TEnum>(JsonNamingPolicy? namingPolicy) : JsonStringEnumConverter<TEnum>(namingPolicy, allowIntegerValues: false)
    where TEnum : struct, Enum
{
    /// <summary>Names as declared; a source-generated context creates its converters through this constructor.</summary>
    public NamedOnly()
        : this(namingPolicy: null)
    {
    }
}

/// <summary>Enums by their kebab-case names only (<c>moved-aside</c>), numbers refused.</summary>
internal sealed class KebabNamedOnly<TEnum>() : NamedOnly<TEnum>(JsonNamingPolicy.KebabCaseLower)
    where TEnum : struct, Enum;
