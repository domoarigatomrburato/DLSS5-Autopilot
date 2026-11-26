using System.Globalization;
using System.Resources;

namespace Autopilot.Core.Resources;

/// <summary>The words a person reads, looked up by id in <c>Strings.resx</c>.</summary>
internal static class Texts
{
    // Looked up by id (kind, then reason), so a typed accessor per string would add nothing.
    private static readonly ResourceManager _strings = new("Autopilot.Core.Resources.Strings", typeof(Texts).Assembly);

    public static string Text(string id) =>
        _strings.GetString(id, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"no text with id {id} in the resources");

    public static string Format(string id, params object?[] values) =>
        string.Format(CultureInfo.CurrentCulture, Text(id), values);
}
