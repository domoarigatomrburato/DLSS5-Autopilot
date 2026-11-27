using Autopilot.Core.Library;

namespace Autopilot.Core.Tests.Library.Scanning;

/// <summary>Runs a scan and reads back what it said.</summary>
internal static class Scans
{
    public static async Task<LibraryScan> ScanAsync(ScanRequest request, CancellationToken cancellationToken) =>
        await Scanner.ScanAsync(request, null, cancellationToken);

    /// <summary>Scans and returns every progress step in words.</summary>
    public static async Task<string[]> ProgressAsync(ScanRequest request, CancellationToken cancellationToken)
    {
        List<ScanProgress> steps = [];
        _ = await Scanner.ScanAsync(request, new Steps(steps), cancellationToken);
        return [.. steps.Select(step => step.Explain())];
    }

    /// <summary>The games listed, as "name (source)", in order.</summary>
    public static string Listed(LibraryScan scan) => string.Join(", ", scan.Games.Select(game => $"{game.Name} ({game.Source})"));

    /// <summary>The one note of a kind, in words.</summary>
    public static string Note<T>(IEnumerable<LibraryNote> notes)
        where T : LibraryNote => notes.OfType<T>().Single().Explain();

    /// <summary>Collects progress as it is reported, on the reporting thread.</summary>
    private sealed class Steps(List<ScanProgress> steps) : IProgress<ScanProgress>
    {
        public void Report(ScanProgress value) => steps.Add(value);
    }
}
