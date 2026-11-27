using Autopilot.Core.Resources;

namespace Autopilot.Core.Routes;

/// <summary>
/// A sentence about a route, kept as a key into <c>Strings.resx</c> and the values it names, so the
/// app and the command line render the same words from the same decision.
/// </summary>
/// <param name="Key">The text's id in <c>Strings.resx</c>.</param>
/// <param name="Values">The values its placeholders take, in order, already in the form a person reads.</param>
public sealed record RouteMessage(string Key, IReadOnlyList<string> Values)
{
    /// <summary>A message whose text names <paramref name="values"/>.</summary>
    internal RouteMessage(string key, params string[] values)
        : this(key, (IReadOnlyList<string>)values)
    {
    }

    /// <summary>The message in words, in the current UI language.</summary>
    public string Text() => Texts.Format(Key, [.. Values]);
}
