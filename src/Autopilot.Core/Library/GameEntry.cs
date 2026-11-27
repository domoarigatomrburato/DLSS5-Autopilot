using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Library.Emulators;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Library;

/// <summary>One game in the library, as reading it found it.</summary>
public sealed record GameEntry
{
    /// <summary>What the store calls it, else a folder's name.</summary>
    public required string Name { get; init; }

    /// <summary>The game's root folder.</summary>
    public required string Folder { get; init; }

    public required GameSource Source { get; init; }

    /// <summary>The executable the tool installs beside, or null when the folder holds none.</summary>
    public string? Executable { get; init; }

    /// <summary>Every executable found, best first, as full paths.</summary>
    public IReadOnlyList<string> Candidates { get; init; } = [];

    /// <summary>The architecture, or null when it is not known.</summary>
    public Bitness? Bitness { get; init; }

    /// <summary>The graphics API in use: the person's choice, else what the executable said.</summary>
    public GraphicsApi Api { get; init; }

    /// <summary>Why <see cref="Api"/>, in words.</summary>
    public string ApiExplanation { get; init; } = "";

    /// <summary>What the executable said, before any choice; null when it was not read.</summary>
    public ApiDecision? Detected { get; init; }

    /// <summary>Whether <see cref="Api"/> is the person's choice.</summary>
    public bool ApiSetByHand { get; init; }

    /// <summary>Why the executable could not be read; null when it was.</summary>
    public GameReadError? Error { get; init; }

    /// <summary>Windows refuses to open the executable, so its architecture and imports are not known.</summary>
    public bool Protected { get; init; }

    /// <summary>The folder an earlier install of ours wrote to, only when it is not the executable's folder.</summary>
    public string? InstallFolder { get; init; }

    /// <summary>What install's record says, or null when there is none.</summary>
    public InstallState? Install { get; init; }

    /// <summary>The emulator the executable is, or null when it is none.</summary>
    public EmulatorProfile? Emulator { get; init; }

    /// <summary>Why the game cannot be set up automatically, or null when it can.</summary>
    public string? Unsupported => Executable is null ? Texts.Text("GameUnsupportedNoExecutable") : Error?.Explanation ?? UnsupportedReading();

    /// <summary>
    /// The folder is one Windows keeps for store games (a path segment exactly <c>XboxGames</c> or
    /// <c>WindowsApps</c>, any case). It says nothing about write access.
    /// </summary>
    public bool InLockedStoreFolder =>
        Folder.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment.Equals("XboxGames", StringComparison.OrdinalIgnoreCase) || segment.Equals("WindowsApps", StringComparison.OrdinalIgnoreCase));

    private string? UnsupportedReading()
    {
        if (Protected && (Bitness is null || !GameChoices.IsChoosable(Api)))
        {
            return Texts.Text("GameUnsupportedProtected");
        }

        if (Bitness is null)
        {
            return Texts.Text("GameUnsupportedNoBitness");
        }

        return Api == GraphicsApi.DX8 && Bitness == Executables.Bitness.Bits64 ? Texts.Text("GameUnsupportedDx8On64") : null;
    }
}
