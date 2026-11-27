using Autopilot.Core.Library.Stores;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Library;

/// <summary>How far a scan has got.</summary>
public abstract record ScanProgress
{
    private protected ScanProgress()
    {
    }

    /// <summary>The step in words.</summary>
    public abstract string Explain();
}

/// <summary>A source is about to be asked for its games.</summary>
public sealed record ScanningSource(GameSource Source) : ScanProgress
{
    public override string Explain() => Texts.Format("ScanSource", Sources.Label(Source));
}

/// <summary>Game <paramref name="Number"/> of <paramref name="Total"/> is about to be read.</summary>
public sealed record ReadingGame(int Number, int Total, string Name) : ScanProgress
{
    public override string Explain() => Texts.Format("ScanReadingGame", Number, Total, Name);
}
