using Autopilot.Core.Library;
using Microsoft.Extensions.Time.Testing;

namespace Autopilot.Core.Tests.Library.Scanning;

/// <summary>The person's choices, by folder, ignoring case; a folder can be made slow to read.</summary>
internal sealed class FakeChoices(FakeTimeProvider clock) : IGameChoices
{
    private readonly Dictionary<string, GameChoices> _choices = [with(StringComparer.OrdinalIgnoreCase)];

    private readonly Dictionary<string, TimeSpan> _delays = [with(StringComparer.OrdinalIgnoreCase)];

    public void Set(string folder, GameChoices choices) => _choices[Normal(folder)] = choices;

    /// <summary>Reading the game in <paramref name="folder"/> takes <paramref name="duration"/> by the clock.</summary>
    public void Slow(string folder, TimeSpan duration) => _delays[Normal(folder)] = duration;

    public GameChoices? ForFolder(string gameFolder)
    {
        if (_delays.TryGetValue(Normal(gameFolder), out TimeSpan delay))
        {
            clock.Advance(delay);
        }

        return _choices.GetValueOrDefault(Normal(gameFolder));
    }

    private static string Normal(string folder) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
}
