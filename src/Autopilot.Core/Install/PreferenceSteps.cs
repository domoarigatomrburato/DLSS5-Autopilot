using Autopilot.Core.Platform;
using Autopilot.Core.Resources;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install;

/// <summary>
/// A plan's graphics preferences: our token added to the program's value, or the previous install's record kept when our
/// own value is found again. A preference the person set is left alone and never recorded, with a note. A registry that
/// cannot be read is a warning, and the previous install's record, if any, is carried forward.
/// </summary>
/// <param name="user">The current user's registry.</param>
/// <param name="paths">The game folder.</param>
/// <param name="prior">The previous install.</param>
/// <param name="texts">Where the notes and warnings go.</param>
/// <param name="logger">Where the decisions are logged.</param>
internal sealed partial class PreferenceSteps(IUserRegistry user, InstallPaths paths, PriorInstall prior, SharedTexts texts, ILogger logger)
{
    /// <summary>The actions of <paramref name="step"/>.</summary>
    public IReadOnlyList<InstallAction> Preference(SetGraphicsPreference step)
    {
        string program = paths.Full(step.Path);
        GraphicsPreferenceEdit? before = prior.Record?.GraphicsPreferences.FirstOrDefault(edit => SharedPaths.Same(edit.Program, program));
        string? name;
        string? current;
        try
        {
            name = GraphicsPreferences.NameOf(user, program);
            current = name is null ? null : user.ReadString(GraphicsPreferences.Key, name);
        }
        catch (Exception refused) when (RegistryChange.IsRefusal(refused))
        {
            LogUnreadable(logger, refused, program);
            texts.Warn(GraphicsPreferences.Refused(program, refused));
            return Kept(step, before);
        }

        return GraphicsPreferences.Chosen(current) is { } chosen
            ? Chosen(step, program, chosen, before)
            : [new PreferenceAction(step.Path, new(name ?? program, current, GraphicsPreferences.With(current)), Writes: true, step.Other, user)];
    }

    // Our own value found again keeps its first record; any other choice is the person's.
    private IReadOnlyList<InstallAction> Chosen(SetGraphicsPreference step, string program, string chosen, GraphicsPreferenceEdit? before)
    {
        if (chosen == GraphicsPreferences.High && before is not null)
        {
            return Kept(step, before);
        }

        LogPersonal(logger, program, chosen);
        texts.Note(chosen == GraphicsPreferences.High
            ? Texts.Format("InstallPreferenceKept", Path.GetFileName(program))
            : Texts.Format("InstallPreferenceKeptNotHigh", Path.GetFileName(program), GraphicsPreferences.Shown(step.Other)));
        return [];
    }

    private IReadOnlyList<InstallAction> Kept(SetGraphicsPreference step, GraphicsPreferenceEdit? before) =>
        before is null ? [] : [new PreferenceAction(step.Path, before, Writes: false, step.Other, user)];

    [LoggerMessage(Level = LogLevel.Information, Message = "{Program}: your own Windows graphics setting ({Chosen}) is kept")]
    private static partial void LogPersonal(ILogger logger, string program, string chosen);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The Windows graphics setting of {Program} cannot be read")]
    private static partial void LogUnreadable(ILogger logger, Exception failure, string program);
}
