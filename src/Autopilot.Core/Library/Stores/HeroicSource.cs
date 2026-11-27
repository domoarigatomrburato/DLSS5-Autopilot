using System.Text.Json;
using Autopilot.Core.Platform;
using Autopilot.Core.Storage;

namespace Autopilot.Core.Library.Stores;

/// <summary>
/// The games the Heroic launcher installed through Legendary (Epic), its GOG store and Nile (Amazon),
/// from their <c>installed.json</c> files under <c>%APPDATA%\heroic</c>.
/// </summary>
public sealed class HeroicSource(HostFolders folders) : IGameSource
{
    // Legendary: { "<app>": { "title", "install_path", "is_dlc" } }; GOG: { "installed": [{ "install_path" }] }; Nile: [{ "path" }].
    private static readonly string[][] _lists =
    [
        ["legendaryConfig", "legendary", "installed.json"],
        ["gog_store", "installed.json"],
        ["nile_config", "nile", "installed.json"],
    ];

    public GameSource Source => GameSource.Heroic;

    public async Task<IReadOnlyList<StoreGame>> FindAsync(CancellationToken cancellationToken)
    {
        if (folders.AppData is not { } appData)
        {
            return [];
        }

        FoundGames found = new(GameSource.Heroic);
        foreach (string[] list in _lists)
        {
            using JsonDocument? installed = await StoreFiles.ReadJsonAsync(Path.Join([appData, "heroic", .. list]), cancellationToken);
            if (installed is not null)
            {
                AddAll(Records(installed.RootElement), found);
            }
        }

        return found.Games;
    }

    private static void AddAll(IEnumerable<JsonElement> records, FoundGames found)
    {
        foreach (JsonElement record in records.Where(record => UntrustedJson.Flag(record, "is_dlc") != true))
        {
            found.Add(UntrustedJson.Text(record, "title"), UntrustedJson.Text(record, "install_path") ?? UntrustedJson.Text(record, "path"));
        }
    }

    // Each launcher keeps its own shape; any of them is read from any file.
    private static IEnumerable<JsonElement> Records(JsonElement root) => root.ValueKind switch
    {
        JsonValueKind.Array => root.EnumerateArray(),
        JsonValueKind.Object when root.TryGetProperty("installed", out JsonElement list) && list.ValueKind == JsonValueKind.Array => list.EnumerateArray(),
        JsonValueKind.Object => root.EnumerateObject().Select(property => property.Value),
        _ => [],
    };
}
