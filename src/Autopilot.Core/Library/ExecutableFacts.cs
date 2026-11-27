using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Library;

/// <summary>What a game's executable says: its architecture, whether Windows protects it, and its graphics API.</summary>
internal static class ExecutableFacts
{
    /// <summary>
    /// Reads <paramref name="executable"/> once into <paramref name="entry"/>. A file that is not an
    /// executable this tool reads is the entry's error. A protected one takes its architecture from
    /// the person's choice; a readable one ignores that choice.
    /// </summary>
    public static async Task<GameEntry> ReadAsync(GameEntry entry, string executable, GameChoices? choices, ApiSearch search, CancellationToken cancellationToken)
    {
        ExecutableReading reading;
        try
        {
            reading = await Executable.ReadAsync(executable, cancellationToken);
        }
        catch (PeException e)
        {
            return entry with { Error = new GameReadError(GameReadFailure.Unreadable, e.Message) };
        }

        ApiDecision detected = await GraphicsApiDecision.DecideAsync(executable, reading, search, cancellationToken);
        return WithApi(entry, detected, choices?.ChosenApi) with
        {
            Protected = reading is ProtectedExecutable,
            Bitness = reading is ReadableExecutable readable ? readable.Bitness : choices?.Bitness,
        };
    }

    // The import table can lie: R.U.S.E. links D3D11 and renders with D3D9, so a choice made by hand wins.
    private static GameEntry WithApi(GameEntry entry, ApiDecision detected, GraphicsApi? chosen) =>
        chosen is { } api
            ? entry with { Api = api, ApiExplanation = Texts.Format("GameApiSetByHand", ApiName(detected.Api)), Detected = detected, ApiSetByHand = true }
            : entry with { Api = detected.Api, ApiExplanation = detected.Explanation, Detected = detected };

    private static string ApiName(GraphicsApi api) => api switch
    {
        GraphicsApi.Unknown => Texts.Text("GameApiUnknown"),
        GraphicsApi.Dxgi => Texts.Text("GameApiDxgi"),
        _ => api.ToString(),
    };
}
