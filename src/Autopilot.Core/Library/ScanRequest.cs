using Autopilot.Core.Library.Stores;

namespace Autopilot.Core.Library;

/// <summary>What a scan reads, and what it already knows.</summary>
/// <param name="Sources">The stores and folder searches, asked on every scan.</param>
/// <param name="FullScanOnlySources">Sources a full scan asks and a quick scan does not (the emulator search).</param>
/// <param name="HandAddedFolders">The folders the person added, as remembered; added back after every full scan.</param>
/// <param name="Reading">What reading each game needs.</param>
/// <param name="Clock">Times each game's reading.</param>
/// <param name="Known">The games a previous scan found, for a quick scan; null for a full scan.</param>
public sealed record ScanRequest(
    IReadOnlyList<IGameSource> Sources,
    IReadOnlyList<IGameSource> FullScanOnlySources,
    IReadOnlyList<string> HandAddedFolders,
    GameReading Reading,
    TimeProvider Clock,
    IReadOnlyList<GameEntry>? Known);
