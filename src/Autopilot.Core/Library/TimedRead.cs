namespace Autopilot.Core.Library;

/// <summary>Reads one game for a scan or a cache reload, keeping what the log should say about it.</summary>
internal static class TimedRead
{
    /// <summary>A game whose reading takes this long or longer gets a <see cref="SlowRead"/> note.</summary>
    private static readonly TimeSpan _slow = TimeSpan.FromSeconds(1);

    /// <summary>Runs <paramref name="read"/>; when it gives a game, adds its notes to <paramref name="notes"/>, and a <see cref="SlowRead"/> when it was slow.</summary>
    public static async Task<GameRead?> RunAsync(TimeProvider clock, List<LibraryNote> notes, Func<Task<GameRead?>> read)
    {
        long started = clock.GetTimestamp();
        GameRead? result = await read();
        TimeSpan took = clock.GetElapsedTime(started);
        if (result is not null)
        {
            notes.AddRange(result.Notes);
            if (took >= _slow)
            {
                notes.Add(new SlowRead(result.Entry.Name, took));
            }
        }

        return result;
    }
}
