using Autopilot.Core.Gpu;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Components;

/// <summary>The <c>nvngx_dlssnr.dll</c> builds to try for a card, in order.</summary>
/// <param name="Entries">The candidates, in the order they are tried.</param>
/// <param name="Pinned">Whether the person chose a label: then it is the only candidate.</param>
/// <param name="WantedMissing">The chosen label is not on the list and the newest stands in, for the caller to say.</param>
public sealed record DlssnrCandidates(IReadOnlyList<BuildEntry> Entries, bool Pinned, bool WantedMissing);

/// <summary>How a build's file stands against the card.</summary>
public enum DlssnrFit
{
    /// <summary>The file carries code for the card's architecture.</summary>
    Compatible,

    /// <summary>The file carries no code for the card's architecture.</summary>
    Incompatible,

    /// <summary>No architecture could be read from the file, or the card is not known.</summary>
    CannotTell,
}

/// <summary>A build's file checked against the card.</summary>
/// <param name="Fit">How it stands.</param>
/// <param name="Supported">The architectures (sm) the file carries code for.</param>
/// <param name="Message">The result in words, for the log; for <see cref="DlssnrFit.CannotTell"/> the install's warning.</param>
public sealed record DlssnrCheck(DlssnrFit Fit, IReadOnlySet<int> Supported, string Message);

/// <summary>What the candidates loop ended with.</summary>
public abstract record DlssnrOutcome
{
    private protected DlssnrOutcome()
    {
    }
}

/// <summary>A build to place.</summary>
/// <param name="Entry">The build.</param>
/// <param name="Check">Its file against the card.</param>
/// <param name="Skipped">The candidates before it that did not match the card, in order.</param>
public sealed record DlssnrChosen(BuildEntry Entry, DlssnrCheck Check, IReadOnlyList<BuildEntry> Skipped) : DlssnrOutcome
{
    /// <summary>The install's warning: a check that could not tell, or a mismatch the person overrode; null otherwise.</summary>
    public string? Warning => Check.Fit switch
    {
        DlssnrFit.CannotTell => Check.Message,
        DlssnrFit.Incompatible => Texts.Format("DlssnrOverridden", Entry.Label),
        _ => null,
    };

    /// <summary>The note naming the candidates skipped as incompatible; null when none was.</summary>
    public string? SkippedNote => Skipped.Count > 0 ? Texts.Format("DlssnrSkipped", Labels(Skipped)) : null;

    internal static string Labels(IEnumerable<BuildEntry> entries) => string.Join(", ", entries.Select(entry => entry.Label));
}

/// <summary>No candidate can be placed: the install stops before writing.</summary>
/// <param name="Message">Why, in words.</param>
/// <param name="Tried">The candidates checked, in order.</param>
public sealed record DlssnrRefused(string Message, IReadOnlyList<BuildEntry> Tried) : DlssnrOutcome;

/// <summary>
/// The <c>nvngx_dlssnr.dll</c> build for the card. The order prefers, by label, the build made for
/// the card's architecture (an RTX 50 must get NVIDIA's original, though a patched build may list
/// sm 120 too), then the mirror's order; the file's fatbin records decide, not the label.
/// </summary>
public static class DlssnrBuilds
{
    /// <summary>The preferred labels per architecture, in order; first matching row wins.</summary>
    private static readonly (int MinSm, int MaxSm, string[] Labels)[] _preferred =
    [
        (120, int.MaxValue, ["310.8.0"]),
        (89, 89, ["310.8.0-RTX40"]),
        (86, 86, ["310.8.SF-v2", "310.8.SF"]),
        (75, 75, ["310.8.SF-v2", "310.8.SF"]),
    ];

    /// <summary>
    /// The builds to try: with a label chosen, that build alone (the newest when it is not on the
    /// list, marked for the caller to say); on Auto, the card's preferred labels, then every other
    /// build newest first.
    /// </summary>
    /// <param name="catalog">The build lists.</param>
    /// <param name="sm">The card's architecture; null when the card is not known.</param>
    /// <param name="chosenLabel">The label the person chose; null on Auto.</param>
    /// <exception cref="ComponentException">The list has no <c>nvngx_dlssnr.dll</c> builds.</exception>
    public static DlssnrCandidates Candidates(BuildCatalog catalog, int? sm, string? chosenLabel)
    {
        if (!string.IsNullOrWhiteSpace(chosenLabel))
        {
            PickedBuild picked = catalog.Pick(BuildFamily.Dlssnr, chosenLabel);
            return new([picked.Entry], Pinned: true, picked.WantedMissing);
        }

        IReadOnlyList<BuildEntry> entries = catalog.Entries(BuildFamily.Dlssnr);
        if (entries.Count == 0)
        {
            throw ComponentException.BuildListUnavailable(BuildFamily.Dlssnr);
        }

        BuildEntry[] first = [.. PreferredLabels(sm).SelectMany(label => entries.Where(entry => entry.Label == label))];
        return new([.. first, .. entries.Except(first)], Pinned: false, WantedMissing: false);
    }

    /// <summary>The labels preferred for an architecture, in order; none for an unknown card or one below RTX 20.</summary>
    internal static IReadOnlyList<string> PreferredLabels(int? sm) =>
        sm is { } known ? _preferred.FirstOrDefault(row => known >= row.MinSm && known <= row.MaxSm).Labels ?? [] : [];

    /// <summary>A file's architectures against the card.</summary>
    /// <param name="supported">The sm the file carries code for, as its fatbin records list them.</param>
    /// <param name="card">The card; null when none was detected.</param>
    public static DlssnrCheck Check(IReadOnlySet<int> supported, GpuCard? card)
    {
        if (supported.Count == 0)
        {
            return new(DlssnrFit.CannotTell, supported, Texts.Format("DlssnrCannotVerify", Texts.Text("DlssnrNoArchitectures")));
        }

        string listed = string.Join(", ", supported.Order().Select(Name));
        if (card?.Sm is not { } sm)
        {
            return new(DlssnrFit.CannotTell, supported, Texts.Format("DlssnrCannotVerify", Texts.Format("DlssnrCardUnknown", listed)));
        }

        return supported.Contains(sm)
            ? new(DlssnrFit.Compatible, supported, Texts.Format("DlssnrCompatible", listed))
            : new(DlssnrFit.Incompatible, supported, Texts.Format("DlssnrIncompatible", Name(sm), listed));
    }

    /// <summary>
    /// Tries the candidates in order: each is read (<paramref name="readArchitectures"/> downloads it,
    /// takes the DLL out and reads its fatbin, before anything is placed) and checked against the card.
    /// The first that does not mismatch is chosen; a mismatch is skipped on Auto, and refuses a chosen
    /// label unless <paramref name="placeMismatch"/>, the person's override, is set.
    /// </summary>
    /// <param name="candidates">The builds to try, from <see cref="Candidates"/>.</param>
    /// <param name="card">The card; null when none was detected.</param>
    /// <param name="placeMismatch">Whether the person chose to place a build that does not match the card.</param>
    /// <param name="readArchitectures">Reads a candidate's file: the sm it carries code for.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    public static async Task<DlssnrOutcome> ChooseAsync(
        DlssnrCandidates candidates,
        GpuCard? card,
        bool placeMismatch,
        Func<BuildEntry, CancellationToken, Task<IReadOnlySet<int>>> readArchitectures,
        CancellationToken cancellationToken)
    {
        List<BuildEntry> skipped = [];
        foreach (BuildEntry entry in candidates.Entries)
        {
            DlssnrCheck check = Check(await readArchitectures(entry, cancellationToken), card);
            if (check.Fit != DlssnrFit.Incompatible || placeMismatch)
            {
                return new DlssnrChosen(entry, check, skipped);
            }

            if (candidates.Pinned)
            {
                return new DlssnrRefused(Texts.Format("DlssnrPinnedMismatch", entry.Label, card!.Name, check.Message), [entry]);
            }

            skipped.Add(entry);
        }

        return new DlssnrRefused(Texts.Format("DlssnrNoneMatched", card?.Name, DlssnrChosen.Labels(skipped)), skipped);
    }

    private static string Name(int sm) => CardNames.Family(sm) ?? $"sm_{sm}";
}
