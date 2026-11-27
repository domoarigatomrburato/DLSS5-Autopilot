using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;

namespace Autopilot.Core.Library;

/// <summary>What the person chose by hand for one game folder; null where they left it automatic.</summary>
/// <param name="Api">The graphics API the game really draws with. One that is not DX8, DX9, DX10, DX11, DX12, Vulkan or OpenGL counts as none.</param>
/// <param name="Bitness">The architecture, for an executable Windows refuses to read.</param>
/// <param name="Executable">The executable picked among the candidates, as a full path; used only while that file exists.</param>
public sealed record GameChoices(GraphicsApi? Api, Bitness? Bitness, string? Executable)
{
    /// <summary>The API chosen, when it is one a person can choose.</summary>
    internal GraphicsApi? ChosenApi => Api is { } api && IsChoosable(api) ? api : null;

    /// <summary>Whether an API or an architecture was chosen: both are kept by folder and are lost with the entry.</summary>
    internal bool SetsApiOrBitness => ChosenApi is not null || Bitness is not null;

    /// <summary>Whether a person can choose <paramref name="api"/>: a real API, not "unknown" nor "11 or 12".</summary>
    internal static bool IsChoosable(GraphicsApi api) => api is not (GraphicsApi.Unknown or GraphicsApi.Dxgi);
}

/// <summary>The person's choices, kept per game folder.</summary>
public interface IGameChoices
{
    /// <summary>The choices for <paramref name="gameFolder"/>, matched ignoring case, or null when none were made.</summary>
    GameChoices? ForFolder(string gameFolder);
}
