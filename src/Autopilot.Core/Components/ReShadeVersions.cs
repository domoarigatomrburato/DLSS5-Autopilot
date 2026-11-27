using System.Text.Json;
using System.Text.RegularExpressions;
using Autopilot.Core.Storage;

namespace Autopilot.Core.Components;

/// <summary>The ReShade version each source names: a link on the home page, a tag, a cached installer's name.</summary>
internal static partial class ReShadeVersions
{
    /// <summary>Whether a page holds the add-on installer's link, so a 5xx carrying it is still the answer.</summary>
    public static bool HasSetupLink(string page) => SetupLink().IsMatch(page);

    /// <summary>The version of the first add-on installer link on reshade.me's page.</summary>
    public static ComponentVersion? FromHomePage(string page) =>
        SetupLink().Match(page) is { Success: true } link ? new(link.Groups["version"].Value) : null;

    /// <summary>The newest tag named <c>v&lt;n&gt;(.&lt;n&gt;)+</c> in the API's tag list; nothing from an answer that is not one.</summary>
    public static ComponentVersion? NewestTag(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Array
                ? Newest(document.RootElement.EnumerateArray().Select(tag => VersionTag().Match(UntrustedJson.Text(tag, "name") ?? "")))
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The newest version among cached installer names.</summary>
    public static ComponentVersion? NewestCached(IEnumerable<string> names) => Newest(names.Select(name => CachedInstaller().Match(name)));

    private static ComponentVersion? Newest(IEnumerable<Match> matches) =>
        matches.Where(match => match.Success)
            .Select(match => new ComponentVersion(match.Groups["version"].Value))
            .Aggregate((ComponentVersion?)null, (newest, version) => newest is null || version.CompareTo(newest) == VersionOrder.Newer ? version : newest);

    [GeneratedRegex(@"/downloads/ReShade_Setup_(?<version>[0-9]+(?:\.[0-9]+)+)_Addon\.exe", RegexOptions.CultureInvariant)]
    private static partial Regex SetupLink();

    [GeneratedRegex(@"^v(?<version>[0-9]+(?:\.[0-9]+)+)$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionTag();

    [GeneratedRegex(@"^ReShade_Setup_(?<version>[0-9]+(?:\.[0-9]+)+)_Addon\.exe$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex CachedInstaller();
}
