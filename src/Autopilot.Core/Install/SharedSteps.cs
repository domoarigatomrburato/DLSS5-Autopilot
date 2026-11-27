using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install;

/// <summary>
/// A plan's layer registrations and graphics preferences resolved against the registry, the layer folder and the
/// previous install: read-only, so the preview and the transaction see one answer. It owns what they add to the
/// report's notes and warnings.
/// </summary>
internal sealed class SharedSteps
{
    private readonly SharedTexts _texts = new();

    /// <param name="shared">The places outside the game folder.</param>
    /// <param name="paths">The game folder.</param>
    /// <param name="prior">The previous install.</param>
    /// <param name="header">The plan's header: the game's architecture.</param>
    /// <param name="logger">Where the decisions are logged.</param>
    public SharedSteps(SharedPlaces shared, InstallPaths paths, PriorInstall prior, PlanHeader header, ILogger logger)
    {
        Layers = shared.LayerFolder is { } folder ? new(shared.User, shared.Machine, folder, header.Game.Bitness, _texts, logger) : null;
        Preferences = new(shared.User, paths, prior, _texts, logger);
    }

    /// <summary>The layer registrations; null without a layer folder, which the checks refuse.</summary>
    public LayerSteps? Layers { get; }

    private PreferenceSteps Preferences { get; }

    /// <summary>The actions of <paramref name="step"/>.</summary>
    public Task<IReadOnlyList<InstallAction>> PreferenceAsync(SetGraphicsPreference step) => Task.FromResult(Preferences.Preference(step));

    /// <summary><paramref name="opening"/> with what the steps said added to its notes and warnings.</summary>
    public InstallRecord Annotated(InstallRecord opening) => opening with
    {
        Notes = [.. opening.Notes, .. _texts.Notes],
        Warnings = [.. opening.Warnings, .. _texts.Warnings],
    };
}

/// <summary>What the shared steps add to the report, each text once.</summary>
internal sealed class SharedTexts
{
    private readonly List<string> _notes = [];
    private readonly List<string> _warnings = [];

    public IReadOnlyList<string> Notes => _notes;

    public IReadOnlyList<string> Warnings => _warnings;

    public void Note(string text) => Add(_notes, text);

    public void Warn(string text) => Add(_warnings, text);

    private static void Add(List<string> texts, string text)
    {
        if (!texts.Contains(text))
        {
            texts.Add(text);
        }
    }
}
