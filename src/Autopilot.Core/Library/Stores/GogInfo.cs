using System.Text.Json;

namespace Autopilot.Core.Library.Stores;

/// <summary>What a game folder's <c>goggame-&lt;id&gt;.info</c> says about that id.</summary>
internal sealed record GogInfo(bool IsGame, string? Name, string? PrimaryExecutable)
{
    /// <summary>
    /// The file's answer; null when it is missing or unreadable, which leaves the entry in. DLC has
    /// a root game other than itself, or no primary play task.
    /// </summary>
    public static async Task<GogInfo?> ReadAsync(string folder, string id, CancellationToken cancellationToken)
    {
        using JsonDocument? info = await StoreFiles.ReadJsonAsync(Path.Join(folder, $"goggame-{id}.info"), cancellationToken);
        if (info is null || info.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        JsonElement root = info.RootElement;
        string? rootGame = StoreFiles.Text(root, "rootGameId");
        JsonElement? primary = PrimaryTask(root);
        bool isGame = primary is not null && (string.IsNullOrEmpty(rootGame) || rootGame == id);
        string? executable = primary is { } task && string.Equals(StoreFiles.Text(task, "type"), "FileTask", StringComparison.OrdinalIgnoreCase)
            ? StoreFiles.Text(task, "path")
            : null;
        return new(isGame, StoreFiles.Text(root, "name"), executable);
    }

    private static JsonElement? PrimaryTask(JsonElement root)
    {
        if (!root.TryGetProperty("playTasks", out JsonElement tasks) || tasks.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement task in tasks.EnumerateArray())
        {
            if (StoreFiles.Flag(task, "isPrimary") == true)
            {
                return task;
            }
        }

        return null;
    }
}
