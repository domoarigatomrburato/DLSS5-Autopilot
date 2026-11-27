using Autopilot.Core.Gpu;
using Autopilot.Core.Net;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Components.Mirror;

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
/// <param name="Untrusted">The candidates before it whose file failed its trust check (a pinned digest or NVIDIA's signature), in order.</param>
public sealed record DlssnrChosen(BuildEntry Entry, DlssnrCheck Check, IReadOnlyList<BuildEntry> Skipped, IReadOnlyList<BuildEntry> Untrusted) : DlssnrOutcome
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

    /// <summary>The note naming the candidates skipped as untrusted; null when none was.</summary>
    public string? UntrustedNote => Untrusted.Count > 0 ? Texts.Format("DlssnrUntrusted", Labels(Untrusted)) : null;

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

        IReadOnlyList<BuildEntry> entries = catalog.Require(BuildFamily.Dlssnr);
        BuildEntry[] first = [.. PreferredLabels(sm).SelectMany(label => entries.Where(entry => entry.Label == label))];
        return new([.. first, .. entries.Except(first)], Pinned: false, WantedMissing: false);
    }

    /// <summary>The labels preferred for an architecture, in order; none for an unknown card or one below RTX 20.</summary>
    private static string[] PreferredLabels(int? sm) =>
        sm is { } known ? DlssnrPins.Preferred.FirstOrDefault(row => known >= row.MinSm && known <= row.MaxSm).Labels ?? [] : [];

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
    /// takes the DLL out, runs its contents check, <see cref="BuildEntry.Contents"/>, and reads its
    /// fatbin, before anything is placed) and checked against the card. The first that does not
    /// mismatch is chosen. A mismatch is skipped on Auto, and refuses a chosen label unless
    /// <paramref name="placeMismatch"/>, the person's override, is set. A file that fails its contents
    /// check is skipped on Auto, and always refuses a chosen label: no override places an untrusted file.
    /// </summary>
    /// <param name="candidates">The builds to try, from <see cref="Candidates"/>.</param>
    /// <param name="card">The card; null when none was detected.</param>
    /// <param name="placeMismatch">Whether the person chose to place a build that does not match the card.</param>
    /// <param name="readArchitectures">
    /// Reads a candidate's file: the sm it carries code for. It throws <see cref="NetException"/> with a
    /// <see cref="VerificationFailedDetail"/> when the file fails its contents check; any other failure
    /// ends the loop.
    /// </param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    public static async Task<DlssnrOutcome> ChooseAsync(
        DlssnrCandidates candidates,
        GpuCard? card,
        bool placeMismatch,
        Func<BuildEntry, CancellationToken, Task<IReadOnlySet<int>>> readArchitectures,
        CancellationToken cancellationToken)
    {
        List<BuildEntry> skipped = [];
        List<BuildEntry> untrusted = [];
        foreach (BuildEntry entry in candidates.Entries)
        {
            IReadOnlySet<int> supported;
            try
            {
                supported = await readArchitectures(entry, cancellationToken);
            }
            catch (NetException failure) when (failure.Detail is VerificationFailedDetail)
            {
                if (candidates.Pinned)
                {
                    return new DlssnrRefused(Untrusted(entry), [entry]);
                }

                untrusted.Add(entry);
                continue;
            }

            DlssnrCheck check = Check(supported, card);
            if (check.Fit != DlssnrFit.Incompatible || placeMismatch)
            {
                return new DlssnrChosen(entry, check, skipped, untrusted);
            }

            if (candidates.Pinned)
            {
                return new DlssnrRefused(Texts.Format("DlssnrPinnedMismatch", entry.Label, card!.Name, check.Message), [entry]);
            }

            skipped.Add(entry);
        }

        // Every candidate was skipped, as incompatible or untrusted: the card is blamed only when none was untrusted.
        string cardName = card?.Name ?? Texts.Text("DlssnrNoCard");
        string tried = DlssnrChosen.Labels(candidates.Entries);
        string message = untrusted.Count == 0
            ? Texts.Format("DlssnrNoneMatched", cardName, tried)
            : Texts.Format("DlssnrNoneTrusted", cardName, tried, DlssnrChosen.Labels(untrusted));
        return new DlssnrRefused(message, candidates.Entries);
    }

    // A chosen build whose file failed its contents check: a build with no pin in this release (so it
    // had to carry NVIDIA's signature: NVIDIA's own, or a community build not pinned yet), or a
    // pinned one whose digest did not match.
    private static string Untrusted(BuildEntry entry) =>
        DlssnrPins.Of(entry.Label) is null
            ? Texts.Format("DlssnrNoDigest", entry.Label)
            : Texts.Format("DlssnrDigestMismatch", entry.Label);

    private static string Name(int sm) => CardNames.Family(sm) ?? $"sm_{sm}";
}
