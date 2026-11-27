using Autopilot.Core.Resources;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install;

/// <summary>
/// One uninstall's report, with a record or by known names alike: a line per item, how many it removed or
/// put back, and which items are left.
/// </summary>
/// <param name="logger">Where each line is logged.</param>
internal sealed partial class UninstallReport(ILogger logger)
{
    private readonly List<string> _lines = [];
    private readonly HashSet<string> _left = [with(StringComparer.OrdinalIgnoreCase)];
    private int _removed;

    /// <summary>Says <paramref name="line"/>; when it <paramref name="counts"/>, as an item removed or put back.</summary>
    public void Say(string? line, bool counts = false)
    {
        if (line is null)
        {
            return;
        }

        _lines.Add(line);
        LogLine(logger, line);
        _removed += counts ? 1 : 0;
    }

    /// <summary>Counts the item at <paramref name="path"/> as left, once however often it is named.</summary>
    public void Leave(string path) => _left.Add(RecordPaths.Key(path));

    /// <summary>
    /// Makes <paramref name="change"/> to the item at <paramref name="path"/>. When another program holds its file,
    /// the item is said and counted as left; a file that is no item (<paramref name="isItem"/> false: a log, say)
    /// is only logged.
    /// </summary>
    /// <returns>Whether the change went ahead.</returns>
    /// <exception cref="IOException">The change failed other than by a held file.</exception>
    public async Task<bool> TryAsync(string path, Func<Task> change, bool isItem = true)
    {
        try
        {
            await change();
            return true;
        }
        catch (FileHeldException)
        {
            if (isItem)
            {
                Say(Texts.Format("UninstallLineStuck", path));
                Leave(path);
            }
            else
            {
                LogHeld(logger, path);
            }

            return false;
        }
    }

    /// <summary>What the uninstall came to, as reported so far.</summary>
    public UninstallFinished Finished(bool outsideKept) => new(_removed, [.. _lines], _left.Count) { OutsideKept = outsideKept };

    [LoggerMessage(Level = LogLevel.Information, Message = "Uninstall: {Line}")]
    private static partial void LogLine(ILogger logger, string line);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Uninstall left {Path} in place: another program holds it")]
    private static partial void LogHeld(ILogger logger, string path);
}
