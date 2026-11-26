namespace Autopilot.Core.Graphics;

/// <summary>The graphics API a game draws with, the kind of evidence that decided it, and that evidence in words.</summary>
/// <param name="Api">The API.</param>
/// <param name="Reason">The kind of evidence.</param>
/// <param name="Explanation">The reason in words, naming the evidence (file, table, string, folder).</param>
public sealed record ApiDecision(GraphicsApi Api, ApiReason Reason, string Explanation)
{
    /// <summary>How firmly the API was read, from <see cref="Reason"/>.</summary>
    public ApiConfidence Confidence => Reason switch
    {
        ApiReason.StaticImport or ApiReason.DelayLoad => ApiConfidence.Read,
        ApiReason.AgilitySdk or ApiReason.UnrealPlugin or ApiReason.DlssFiles or ApiReason.SiblingImport => ApiConfidence.Inferred,
        ApiReason.None => ApiConfidence.None,
        _ => ApiConfidence.Guessed,
    };
}
