using System.Text.Json.Serialization;
using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Routes;
using Autopilot.Core.Storage;

namespace Autopilot.Core.Install;

/// <summary>
/// The install record (<c>invidia-claritatis.json</c> in the install folder): the only thing uninstall
/// trusts about what was done. Every path in it is relative to the game folder, with <c>/</c> separators.
/// </summary>
/// <param name="FormatVersion">The record's format; this build knows 1, and never edits a newer one.</param>
/// <param name="ToolVersion">The build that wrote it.</param>
/// <param name="State">Where its lifecycle stands.</param>
/// <param name="InstallFolder">
/// The install folder relative to the game folder, <c>""</c> when they are the same folder: the record sits
/// in the install folder, and this is how its reader finds the game folder its paths are relative to.
/// </param>
/// <param name="Game">What the install was made for.</param>
/// <param name="Route">The route installed.</param>
/// <param name="Choices">The person's settings the install read: every choice needed to repeat it.</param>
/// <param name="Components">Each component's id and the version installed.</param>
/// <param name="Entries">One per path the install touches.</param>
/// <param name="ConfigEdits">Key-level edits to files the person owns.</param>
/// <param name="Registrations">The shared layer registrations this install relies on or created.</param>
/// <param name="GraphicsPreferences">The values written under the Windows graphics preference key.</param>
/// <param name="CreatedFolders">The folders the install created.</param>
/// <param name="Notes">What the install said, for the report and the diagnosis.</param>
/// <param name="Warnings">The warnings it gave.</param>
/// <param name="Skipped">What it skipped.</param>
public sealed record InstallRecord(
    [property: JsonPropertyName(InstallRecord.VersionKey)] int FormatVersion,
    string ToolVersion,
    RecordState State,
    string InstallFolder,
    RecordedGame Game,
    RouteId Route,
    RouteSettings Choices,
    IReadOnlyDictionary<string, string> Components,
    IReadOnlyList<RecordEntry> Entries,
    IReadOnlyList<ConfigEdit> ConfigEdits,
    IReadOnlyList<LayerRegistration> Registrations,
    IReadOnlyList<GraphicsPreferenceEdit> GraphicsPreferences,
    IReadOnlyList<string> CreatedFolders,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Skipped)
{
    /// <summary>The key the format version is stored under.</summary>
    internal const string VersionKey = "version";

    /// <summary>
    /// While an install over an earlier one is open (an update, a reinstall, a route switch): that earlier
    /// install's record, whole, which a rollback saves back; null on a fresh install and once committed.
    /// </summary>
    public InstallRecord? Previous { get; init; }

    /// <summary>The entries standing for the files at their paths: every one but the held files' and the originals put back.</summary>
    internal IEnumerable<RecordEntry> Placed => Entries.Where(entry => entry.IsPlaced);
}

/// <summary>The game an install was made for.</summary>
/// <param name="Executables">The executable file names in the install folder; two executables in one folder share one record.</param>
/// <param name="Bitness">The game's architecture.</param>
/// <param name="Api">The graphics API installed for.</param>
/// <param name="Kind">Where the game came from: a store, an emulator, a folder added by hand.</param>
public sealed record RecordedGame(IReadOnlyList<string> Executables, Bitness Bitness, GraphicsApi Api, GameSource Kind);

/// <summary>One path the install touches.</summary>
/// <param name="Path">Relative to the game folder, <c>/</c> separated, spelled as on disk; compared ignoring case, either slash.</param>
/// <param name="Class">Whether the file stays as written, or its components rewrite it while running.</param>
/// <param name="State">Where the path stands.</param>
/// <param name="Written">What the tool wrote there, known before the write; null when it writes nothing there (a moved-aside file).</param>
/// <param name="Original">The file the tool found there and did not write; null when there was none.</param>
public sealed record RecordEntry(string Path, EntryClass Class, EntryState State, FileFacts? Written, OriginalFile? Original)
{
    /// <summary>
    /// For a <see cref="EntryState.Restored"/> entry whose original was superseded rather than put back: which
    /// superseded name of the path holds it, counted from 1 (<c>.invidia-claritatis-superseded</c>, then <c>.2</c> ...).
    /// The name is derived from the path, never stored.
    /// </summary>
    public int? Superseded { get; init; }

    /// <summary>Whether the entry stands for the file at its path: neither a held file beside it nor an original put back.</summary>
    internal bool IsPlaced => State is not (EntryState.Held or EntryState.Restored);
}

/// <summary>Whether a file the tool placed is expected to stay as written.</summary>
public enum EntryClass
{
    /// <summary>A binary the tool placed; expected to stay as written.</summary>
    Component,

    /// <summary>A file its components rewrite while running (<c>ReShade.ini</c>, <c>OptiScaler.ini</c> ...).</summary>
    Config,
}

/// <summary>Where one path of the install stands.</summary>
public enum EntryState
{
    /// <summary>Named, maybe not yet written.</summary>
    Pending,

    /// <summary>Written.</summary>
    Written,

    /// <summary>The game's own file, moved out of the way under its backup name.</summary>
    MovedAside,

    /// <summary>Its backup could not be put back; the backup stays.</summary>
    RestoreFailed,

    /// <summary>It could not be removed (locked).</summary>
    Stuck,

    /// <summary>
    /// A file of the previous install, renamed beside itself to <c>&lt;path&gt;.invidia-claritatis-held</c>
    /// until the install commits (then deleted) or rolls back (then renamed back).
    /// </summary>
    Held,

    /// <summary>
    /// A game's original this install renamed from its backup name: back under its path, or, when
    /// <see cref="RecordEntry.Superseded"/> is set, to a superseded name. Dropped from the record at commit;
    /// a rollback renames it back to its backup name.
    /// </summary>
    Restored,
}

/// <summary>A file's content as the record knows it.</summary>
/// <param name="Sha256">The SHA-256 of its bytes, lowercase hex.</param>
/// <param name="Size">Its length in bytes.</param>
public sealed record FileFacts(string Sha256, long Size)
{
    /// <summary>The facts of the file at <paramref name="path"/>; null when no file is there.</summary>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The file cannot be opened.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static async Task<FileFacts?> OfAsync(string path, CancellationToken cancellationToken)
    {
        FileInfo file = new(path);
        return file.Exists ? new(await FileDigest.Sha256Async(path, cancellationToken), file.Length) : null;
    }

    /// <summary>Whether <paramref name="other"/> is the same content: same size, same hash whatever its case.</summary>
    internal bool Matches(FileFacts? other) => other is not null && Size == other.Size && FileDigest.SameHash(Sha256, other.Sha256);

    /// <summary>Whether the file at <paramref name="path"/> is there with exactly this content.</summary>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The file cannot be opened.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal Task<bool> IsAtAsync(string path, CancellationToken cancellationToken) => FileDigest.HoldsAsync(path, Sha256, Size, cancellationToken);
}

/// <summary>
/// The file the tool found at a path and did not write. Its backup's name is derived from the path
/// and <paramref name="Backup"/>, never stored, so a record cannot point a restore at another file.
/// </summary>
/// <param name="Facts">Its hash and size, taken before the backup.</param>
/// <param name="Attributes">Its attributes; only those a restore sets are kept.</param>
/// <param name="LastWriteTimeUtc">Its modified time.</param>
/// <param name="Backup">Which backup name holds it.</param>
public sealed record OriginalFile(FileFacts Facts, FileAttributes Attributes, DateTime LastWriteTimeUtc, BackupKind Backup)
{
    /// <summary>The attributes a restore sets; any other bit (a cloud file's, say) is not kept.</summary>
    internal const FileAttributes Restored = FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System | FileAttributes.Archive;

    /// <summary>The attributes a restore puts back (read-only, hidden, system, archive); others are dropped.</summary>
    public FileAttributes Attributes { get; init => field = value & Restored; } = Attributes & Restored;

    /// <summary>The suffix the backup's name adds to the path.</summary>
    internal string Suffix => SuffixOf(Backup);

    /// <summary>The suffix <paramref name="kind"/>'s backup name adds to the path.</summary>
    internal static string SuffixOf(BackupKind kind) => kind == BackupKind.Dlss5Off ? KnownFiles.DlssOffSuffix : KnownFiles.BackupSuffix;
}

/// <summary>Which name a game's original is kept under, beside it.</summary>
public enum BackupKind
{
    /// <summary><c>&lt;path&gt;.invidia-claritatis-backup</c>.</summary>
    Backup,

    /// <summary><c>&lt;path&gt;.dlss5-off</c>: the game's <c>d3dcompiler_47.dll</c>, moved aside.</summary>
    Dlss5Off,
}

/// <summary>One key edited in a file the person owns, recorded so only that key is put back.</summary>
/// <remarks>M8's emulator edits widen it to config files outside the game folder; today the file is inside it.</remarks>
/// <param name="File">The file, relative to the game folder, <c>/</c> separated.</param>
/// <param name="Section">The section holding the key; null for a file without sections.</param>
/// <param name="Key">The key.</param>
/// <param name="OriginalLine">The key's line as it was, exactly; null when the key was absent.</param>
/// <param name="SectionAdded">Whether the install added the section.</param>
/// <param name="KeyAdded">Whether the install added the key.</param>
/// <param name="Value">The value written.</param>
/// <param name="EndedWithNewline">Whether the file ended with a newline before the edit.</param>
public sealed record ConfigEdit(string File, string? Section, string Key, string? OriginalLine, bool SectionAdded, bool KeyAdded, string Value, bool EndedWithNewline);

/// <summary>A shared layer registration this install relies on or created.</summary>
/// <param name="Layer">Which layer.</param>
/// <param name="Reused">A foreign layer reused: never removed.</param>
/// <param name="Manifest">The full path of the manifest the registration names.</param>
public sealed record LayerRegistration(RegisteredLayer Layer, bool Reused, string Manifest);

/// <summary>The shared layers an install can register.</summary>
public enum RegisteredLayer
{
    /// <summary>The 64-bit Vulkan layer.</summary>
    VulkanLayer64,

    /// <summary>The 32-bit Vulkan layer.</summary>
    VulkanLayer32,

    /// <summary>The OpenXR API layer (64-bit games only).</summary>
    OpenXr,
}

/// <summary>One value written under the Windows graphics preference key.</summary>
/// <param name="Program">The program's full path, the value's name.</param>
/// <param name="Before">The value's text before the install; null when there was none.</param>
/// <param name="Written">The text written.</param>
public sealed record GraphicsPreferenceEdit(string Program, string? Before, string Written);
