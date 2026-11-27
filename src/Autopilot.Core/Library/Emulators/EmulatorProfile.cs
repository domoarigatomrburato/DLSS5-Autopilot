using Autopilot.Core.Graphics;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Library.Emulators;

/// <summary>
/// A known emulator: what it is called, which system it plays, the executables it ships as, and
/// what it draws with. Its name and system are proper names; its hint and note are the person's
/// words, from the resources.
/// </summary>
public sealed record EmulatorProfile
{
    // drawsWith is Direct3D 11 or 12 (Dxgi) when it can present through Direct3D, else what it draws with.
    internal EmulatorProfile(string key, string name, string system, GraphicsApi drawsWith, bool hasNote, params string[] executables)
    {
        Key = key;
        Name = name;
        System = system;
        Fallback = drawsWith == GraphicsApi.Dxgi ? null : drawsWith;
        HasNote = hasNote;
        Executables = executables;
    }

    /// <summary>What the emulator is called, as its makers spell it.</summary>
    public string Name { get; }

    /// <summary>The system it plays.</summary>
    public string System { get; }

    /// <summary>The executable file names it ships as; any case matches.</summary>
    public IReadOnlyList<string> Executables { get; }

    /// <summary>
    /// What it draws with when it cannot present through Direct3D: Vulkan, or OpenGL for the
    /// emulators that offer nothing else. Null for one that can present through Direct3D.
    /// </summary>
    public GraphicsApi? Fallback { get; }

    /// <summary>Whether it can present through Direct3D 11 or 12, so the Direct3D routes apply.</summary>
    public bool PresentsThroughDirect3D => Fallback is null;

    /// <summary>Which renderer setting the person should pick in the emulator.</summary>
    public string Hint => Texts.Text($"EmulatorHint{Key}");

    /// <summary>What else helps with this emulator, or null when there is nothing to add.</summary>
    public string? Note => HasNote ? Texts.Text($"EmulatorNote{Key}") : null;

    /// <summary>How an entry found as this emulator is named: <c>&lt;name&gt; (&lt;system&gt;)</c>.</summary>
    public string Title => Texts.Format("EmulatorTitle", Name, System);

    /// <summary>The id its texts and its config reader are keyed by.</summary>
    internal string Key { get; }

    private bool HasNote { get; }
}
