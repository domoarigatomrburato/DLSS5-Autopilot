using Autopilot.Core.Resources;

namespace Autopilot.Core.Library.Protection;

/// <summary>Whether a game folder holds an anti-cheat.</summary>
public enum AntiCheatVerdict
{
    /// <summary>Every folder was read and none holds an anti-cheat.</summary>
    None,

    /// <summary>A marker file was found.</summary>
    Present,

    /// <summary>A folder could not be read, so there is no answer: callers treat it as <see cref="Present"/>.</summary>
    Unknown,
}

/// <summary>What <see cref="AntiCheat.DetectAsync"/> found.</summary>
/// <param name="Verdict">The answer.</param>
/// <param name="Products">The products found, sorted and unique.</param>
/// <param name="Evidence">The names of the files the products rest on, sorted, unique, at most four.</param>
public sealed record AntiCheatFinding(AntiCheatVerdict Verdict, IReadOnlyList<string> Products, IReadOnlyList<string> Evidence)
{
    /// <summary>
    /// The warning to show, naming the files it rests on. Empty when <see cref="Verdict"/> is
    /// <see cref="AntiCheatVerdict.None"/>.
    /// </summary>
    public string Explain()
    {
        string text = Verdict switch
        {
            AntiCheatVerdict.None => string.Empty,
            AntiCheatVerdict.Present => Texts.Format("AntiCheatWarning", string.Join(", ", Products)),
            AntiCheatVerdict.Unknown => Texts.Text("AntiCheatUnknown"),
            _ => throw new InvalidOperationException($"no such verdict: {Verdict}"),
        };
        return text.Length > 0 && Evidence.Count > 0
            ? text + Texts.Format("AntiCheatFound", string.Join(", ", Evidence))
            : text;
    }
}
