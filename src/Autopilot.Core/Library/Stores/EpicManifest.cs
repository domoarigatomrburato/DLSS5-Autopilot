using System.Text.Json;
using Autopilot.Core.Storage;

namespace Autopilot.Core.Library.Stores;

/// <summary>What one Epic Games Launcher <c>.item</c> manifest says, read as untrusted JSON.</summary>
internal sealed record EpicManifest(string AppName, string? DisplayName, string? InstallLocation, string? LaunchExecutable, bool IsMainGame)
{
    /// <summary>The manifest, or null when it is malformed or not a game: a plugin, Unreal Engine itself, an add-on or an incomplete install.</summary>
    public static EpicManifest? From(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object || !IsGame(item))
        {
            return null;
        }

        string appName = UntrustedJson.Text(item, "AppName") ?? "";
        string? main = UntrustedJson.Text(item, "MainGameAppName");
        return new(
            appName,
            UntrustedJson.Text(item, "DisplayName"),
            UntrustedJson.Text(item, "InstallLocation"),
            UntrustedJson.Text(item, "LaunchExecutable"),
            appName.Length > 0 && string.Equals(appName, main, StringComparison.OrdinalIgnoreCase));
    }

    // Older manifests carry none of the flags, and stay.
    private static bool IsGame(JsonElement item) =>
        UntrustedJson.Flag(item, "bIsApplication") != false
        && UntrustedJson.Flag(item, "bIsExecutable") != false
        && UntrustedJson.Flag(item, "bIsIncompleteInstall") != true
        && !IsAddOn(item)
        && !IsEnginePlugin(item);

    // An add-on that cannot be launched on its own lives in its game's folder.
    private static bool IsAddOn(JsonElement item)
    {
        List<string> categories = Strings(item, "AppCategories");
        return categories.Contains("addons", StringComparer.OrdinalIgnoreCase) && !categories.Contains("addons/launchable", StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsEnginePlugin(JsonElement item) =>
        Strings(item, "AppCategories").Any(category => category.Equals("plugins", StringComparison.OrdinalIgnoreCase) || category.Equals("plugins/engine", StringComparison.OrdinalIgnoreCase))
        || Strings(item, "CompatibleApps").Any(app => app.StartsWith("UE_", StringComparison.OrdinalIgnoreCase))
        || (UntrustedJson.Text(item, "TechnicalType")?.Contains("plugins/engine", StringComparison.OrdinalIgnoreCase) ?? false);

    private static List<string> Strings(JsonElement item, string name) =>
        [.. UntrustedJson.Array(item, name).Where(entry => entry.ValueKind == JsonValueKind.String).Select(entry => entry.GetString()!)];
}
