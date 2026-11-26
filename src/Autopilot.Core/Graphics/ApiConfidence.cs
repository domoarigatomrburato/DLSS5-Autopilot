namespace Autopilot.Core.Graphics;

/// <summary>How firmly a graphics API was read, following from the kind of evidence.</summary>
public enum ApiConfidence
{
    /// <summary>No evidence.</summary>
    None,

    /// <summary>A graphics DLL in the executable's own tables.</summary>
    Read,

    /// <summary>Files beside the executable decide.</summary>
    Inferred,

    /// <summary>An engine layout or a name in the bytes: a guess, which the person should confirm.</summary>
    Guessed,
}
