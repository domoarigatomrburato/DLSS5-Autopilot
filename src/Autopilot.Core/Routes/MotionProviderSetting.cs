using Autopilot.Core.Components.Catalog;

namespace Autopilot.Core.Routes;

/// <summary>
/// The motion-vector provider a setting or an install record names, read: a provider, the default,
/// or a refusal the install stops on before writing.
/// </summary>
/// <param name="Provider">The provider named; <see langword="null"/> for the default (LumeniteFX's kernel, see <see cref="RouteComponents.ProviderFor"/>) and with a refusal.</param>
/// <param name="Refusal">Why the install stops before writing; <see langword="null"/> when it goes ahead.</param>
public sealed record MotionProviderSetting(MotionProvider? Provider, RouteMessage? Refusal)
{
    /// <summary>The number LumeniteFX's QuantMotion had, which LumeniteFX renamed PocketMV and the feeder does not read yet.</summary>
    internal const int QuantMotion = 4;

    private static readonly MotionProviderSetting _default = new(null, null);

    /// <summary>
    /// Reads the number held: a provider's number is that provider; 4 is a refusal, since LumeniteFX
    /// renamed QuantMotion and the feeder cannot use it; any other number, or none (a value of the
    /// wrong type reads as none), is the default.
    /// </summary>
    /// <param name="number">The number the setting or record holds; <see langword="null"/> when it holds none, or a value of another type.</param>
    public static MotionProviderSetting Read(int? number) => number switch
    {
        QuantMotion => new(null, new RouteMessage("RouteProviderQuantMotion")),
        { } known when Enum.IsDefined((MotionProvider)known) => new((MotionProvider)known, null),
        _ => _default,
    };
}
