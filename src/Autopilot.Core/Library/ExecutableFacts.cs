using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;
using Autopilot.Core.Library.Emulators;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Library;

/// <summary>What a game's executable says: its architecture, whether Windows protects it, and its graphics API.</summary>
internal static class ExecutableFacts
{
    /// <summary>
    /// Reads <paramref name="executable"/> once into <paramref name="entry"/>. A file that is not an
    /// executable this tool reads is the entry's error. A protected one takes its architecture from
    /// the person's choice; a readable one ignores that choice. Without a chosen API, the entry's
    /// emulator decides what the imports really mean.
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

    /// <summary>An API as the person reads it in a reason.</summary>
    public static string ApiName(GraphicsApi api) => api switch
    {
        GraphicsApi.Unknown => Texts.Text("GameApiUnknown"),
        GraphicsApi.Dxgi => Texts.Text("GameApiDxgi"),
        _ => api.ToString(),
    };

    // The import table can lie: R.U.S.E. links D3D11 and renders with D3D9, so a choice made by hand wins.
    private static GameEntry WithApi(GameEntry entry, ApiDecision detected, GraphicsApi? chosen)
    {
        if (chosen is { } api)
        {
            return entry with { Api = api, ApiExplanation = Texts.Format("GameApiSetByHand", ApiName(detected.Api)), Detected = detected, ApiSetByHand = true };
        }

        (GraphicsApi automatic, string explanation) = EmulatorApi.Automatic(detected, entry.Emulator);
        return entry with { Api = automatic, ApiExplanation = explanation, Detected = detected };
    }
}
