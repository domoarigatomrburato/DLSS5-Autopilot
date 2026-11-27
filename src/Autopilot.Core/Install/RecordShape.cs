using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install;

/// <summary>
/// A parsed record checked for shape: every required field there and of its type, no list holding
/// null, format 1, and an install folder that ends the folder the record was found in. Entries,
/// created folders and config edits naming a place outside the game folder are dropped, and of two
/// naming one place the first is kept, each with a line in the log.
/// </summary>
internal static partial class RecordShape
{
    /// <summary>
    /// The record <paramref name="document"/> holds, its unsafe items dropped, as found in <paramref name="folder"/>
    /// with the game folder it names; null when it is the wrong shape.
    /// </summary>
    public static RecordedInstall? Read(JsonObject document, string folder, ILogger logger)
    {
        InstallRecord? record;
        try
        {
            record = document.Deserialize(RecordJson.Record);
        }
        catch (JsonException)
        {
            return null;
        }

        if (record is null || !IsWhole(record) || RecordPaths.GameFolderOf(folder, record.InstallFolder) is not { } gameFolder)
        {
            return null;
        }

        InstallRecord kept = record with
        {
            Entries = Safe(folder, logger, record.Entries, entry => entry.Path, entry => RecordPaths.Key(entry.Path)),
            CreatedFolders = Safe(folder, logger, record.CreatedFolders, created => created, RecordPaths.Key),
            ConfigEdits = Safe(folder, logger, record.ConfigEdits, edit => edit.File, ConfigKey),
        };
        return new RecordedInstall(StateOf(kept.State), kept.Game.Executables) { Record = kept, GameFolder = gameFolder };
    }

    private static InstallState StateOf(RecordState state) => state switch
    {
        RecordState.Installing => InstallState.Installing,
        RecordState.Installed => InstallState.Installed,
        _ => InstallState.Removing,
    };

    // A list holding null, or a record of another format, is the wrong shape: nothing in it can be trusted.
    private static bool IsWhole(InstallRecord record) =>
        record.FormatVersion == InstallRecords.Format
        && record.Game.Executables.All(name => name is not null)
        && record.Components.Values.All(version => version is not null)
        && AllPresent(record.Entries, record.ConfigEdits, record.Registrations, record.GraphicsPreferences)
        && AllPresent(record.CreatedFolders, record.Notes, record.Warnings, record.Skipped);

    private static bool AllPresent(params IEnumerable<IEnumerable<object?>> lists) =>
        lists.All(list => list.All(item => item is not null));

    [LoggerMessage(Level = LogLevel.Warning, Message = "The install record in {Folder} names {Path}, which is ignored: {Reason}")]
    private static partial void LogDropped(ILogger logger, string folder, string path, string reason);

    // One key per file, section and key; a key outside any section is not one in a section named "".
    private static string ConfigKey(ConfigEdit edit) =>
        $"{RecordPaths.Key(edit.File)}\n{(edit.Section is null ? "-" : "+" + edit.Section)}\n{edit.Key}";

    // The first of two items naming one place wins; an item outside the game folder is never followed.
    private static List<T> Safe<T>(string folder, ILogger logger, IReadOnlyList<T> items, Func<T, string> path, Func<T, string> key)
    {
        List<T> kept = [];
        HashSet<string> seen = [with(StringComparer.OrdinalIgnoreCase)];
        foreach (T item in items)
        {
            if (!RecordPaths.IsInside(path(item)))
            {
                LogDropped(logger, folder, path(item), "it is not a path inside the game folder");
            }
            else if (!seen.Add(key(item)))
            {
                LogDropped(logger, folder, path(item), "an earlier entry names the same path");
            }
            else
            {
                kept.Add(item);
            }
        }

        return kept;
    }
}
