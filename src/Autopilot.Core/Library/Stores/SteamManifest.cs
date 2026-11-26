using System.Globalization;
using ValveKeyValue;

namespace Autopilot.Core.Library.Stores;

/// <summary>What one <c>appmanifest_*.acf</c> says about a folder under <c>steamapps\common</c>.</summary>
internal sealed record SteamManifest(string InstallDir, string? Name, bool FullyInstalled, bool IsTool)
{
    private const int FullyInstalledFlag = 4;

    // Steamworks Common Redistributables and SteamVR; Proton and the runtimes go by name, since each version has its own id.
    private static readonly string[] _toolIds = ["228980", "250820"];

    private static readonly string[] _toolPrefixes = ["Proton ", "Steam Linux Runtime", "Steamworks Common", "SteamVR"];

    /// <summary>The manifests of one library's <c>steamapps</c> folder; an unreadable one is skipped.</summary>
    public static async Task<List<SteamManifest>> ReadAllAsync(string steamapps, CancellationToken cancellationToken)
    {
        List<SteamManifest> manifests = [];
        // Filtered by name afterwards: on Windows "*.acf" also matches "appmanifest_1.acf.tmp".
        foreach (string file in Platform.DrivePaths.Files(steamapps))
        {
            string name = Path.GetFileName(file);
            if (name.StartsWith("appmanifest_", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".acf", StringComparison.OrdinalIgnoreCase)
                && await ValveText.ReadAsync(file, cancellationToken) is { } state
                && From(state) is { } manifest)
            {
                manifests.Add(manifest);
            }
        }

        return manifests;
    }

    private static SteamManifest? From(KVObject state)
    {
        string? installDir = ValveText.Text(state, "installdir");
        if (string.IsNullOrWhiteSpace(installDir))
        {
            return null;
        }

        string? name = NonEmpty(ValveText.Text(state, "name"))
            ?? (ValveText.Child(state, "UserConfig") is { IsCollection: true } user ? NonEmpty(ValveText.Text(user, "name")) : null);
        string? appId = ValveText.Text(state, "appid");
        return new(installDir, name, IsFullyInstalled(ValveText.Text(state, "StateFlags")), IsSteamTool(appId, name));
    }

    // Absent flags count as installed (older manifests); flags that do not parse do not.
    private static bool IsFullyInstalled(string? flags) =>
        flags is null || (long.TryParse(flags, NumberStyles.None, CultureInfo.InvariantCulture, out long value) && (value & FullyInstalledFlag) != 0);

    private static bool IsSteamTool(string? appId, string? name) =>
        _toolIds.Contains(appId, StringComparer.Ordinal)
        || (name is not null && _toolPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));

    private static string? NonEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
