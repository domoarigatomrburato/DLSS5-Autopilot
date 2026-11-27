using Autopilot.Core.Net;

namespace Autopilot.Core.Components.Mirror;

/// <summary>
/// The digest of one community <c>nvngx_dlssnr.dll</c> build: the DLL inside the mirror's zip, which
/// no one signs, must have exactly this SHA-256.
/// </summary>
/// <param name="Label">The build's label on the mirror (<c>310.8.SF-v2</c>).</param>
/// <param name="Digest">The DLL's SHA-256, lowercase hex.</param>
/// <param name="Sm">The architectures (sm) the DLL carries code for, as its fatbin records list them.</param>
internal sealed record DlssnrPin(string Label, string Digest, IReadOnlyList<int> Sm)
{
    /// <summary>The check the DLL taken out of the zip must pass.</summary>
    public Sha256Verification Verification { get; } = new(Digest);
}

/// <summary>
/// The <c>nvngx_dlssnr.dll</c> trust rule's data, shipped with the release and reviewed like code. A
/// build whose label has a pin is placed only when its DLL's SHA-256 equals the pin; any other build
/// must carry NVIDIA's signature (NVIDIA's own builds, <c>310.8.0</c> and <c>310.9.16-nvidia</c>, have
/// no pin). So a new NVIDIA build works the day it is published, and a new community build only after
/// a release pins it.
/// </summary>
internal static class DlssnrPins
{
    /// <summary>
    /// The pins, one per community build: each DLL's SHA-256 and the sm its fatbin records list,
    /// measured 2026-11-27 from RankFTW/rhi-repo.
    /// </summary>
    public static IReadOnlyList<DlssnrPin> Table { get; } =
    [
        new("310.8.0-RTX40", "4b8d19bc3eff58a084f5eca7489c921501c203450169fb82ff4f649a4482ba05", [89, 120]),
        new("310.8.SF", "4c5bd1171c7336b4b04fb394de51da285ab6ead6f922d7afdec163f71c319d74", [75, 86, 89, 120]),
        new("310.8.SF-v2", "6eb209e764f39872625debd6abaf45e2bb6322f6f270f781f70c059ae30b3927", [75, 86, 89, 120]),
        new("310.8.Lecram", "f95feb54137ea11979f9b4ec4f00afd84b5c98a5624d3388fbf6a87714a39fcc", [75, 86, 89, 120]),
        new("310.9.16", "cdfa6c1ec784867182164c7836f324f087b5c9f374d0c9fe46f54999feabf3ed", [75, 86, 89, 120]),
    ];

    /// <summary>
    /// The labels preferred per architecture, in order; first matching row wins. By label, because a
    /// patched build can also list sm 120, and an RTX 50 must get NVIDIA's original. A label moves only
    /// when the build has been tried on that card family.
    /// </summary>
    public static IReadOnlyList<(int MinSm, int MaxSm, string[] Labels)> Preferred { get; } =
    [
        (120, int.MaxValue, ["310.9.16-nvidia", "310.8.0"]),
        (89, 89, ["310.8.0-RTX40", "310.9.16"]),
        (86, 86, ["310.8.SF-v2", "310.8.SF"]),
        (75, 75, ["310.8.SF-v2", "310.8.SF"]),
    ];

    /// <summary>The pin for <paramref name="label"/>; <see langword="null"/> when the label has none.</summary>
    public static DlssnrPin? Of(string label) => Table.FirstOrDefault(pin => pin.Label == label);
}
