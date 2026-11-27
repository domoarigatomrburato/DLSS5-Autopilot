using System.Text.Json;
using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;
using Autopilot.Core.Install;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Routes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Autopilot.Core.Tests.Install;

/// <summary>Install records as tests write them: an installed record, entries of ours and swapped ones, planted on disk in the record's own format.</summary>
internal static class Recorded
{
    /// <summary>A placeholder for what was written, where a test does not hash the file.</summary>
    public static FileFacts Unhashed { get; } = new(new string('0', 64), 0);

    /// <summary>A record in state <see cref="RecordState.Installed"/>, for <c>Game.exe</c> on the native route.</summary>
    /// <param name="installFolder">The install folder relative to the game folder; empty when they are one.</param>
    /// <param name="entries">The entries.</param>
    public static InstallRecord Install(string installFolder = "", params IEnumerable<RecordEntry> entries) => new(
        InstallRecords.Format,
        "1.0.0-test",
        RecordState.Installed,
        installFolder,
        new RecordedGame(["Game.exe"], Bitness.Bits64, GraphicsApi.DX12, GameSource.Steam),
        RouteId.Native,
        RouteSettings.Default,
        new Dictionary<string, string>(),
        [.. entries],
        [],
        [],
        [],
        [],
        [],
        [],
        []);

    /// <summary>A component the tool wrote over nothing of the game's.</summary>
    public static RecordEntry Ours(string path, FileFacts? written = null) => new(path, EntryClass.Component, EntryState.Written, written ?? Unhashed, null);

    /// <summary>A component the tool wrote over the game's own file, which it backed up beside it.</summary>
    public static RecordEntry Swapped(string path, FileFacts? original = null, FileFacts? written = null) =>
        Ours(path, written) with { Original = new OriginalFile(original ?? Unhashed, FileAttributes.Normal, new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), BackupKind.Backup) };

    /// <summary>Writes <paramref name="record"/> as the record in <paramref name="installFolder"/>, as the writer would.</summary>
    public static void Plant(string installFolder, InstallRecord record) =>
        TestFiles.Plant(Path.Join(installFolder, KnownFiles.InstallRecord), JsonSerializer.Serialize(record, InstallJsonContext.Default.InstallRecord));

    /// <summary>The reader, logging to <paramref name="logger"/> when given.</summary>
    public static InstallRecords Reader(ILogger<InstallRecords>? logger = null) => new(logger ?? NullLogger<InstallRecords>.Instance, new FakeTimeProvider());

    /// <summary>What the reader finds in <paramref name="folder"/>.</summary>
    public static Task<RecordedInstall?> FindAsync(string folder, CancellationToken cancellationToken) => Reader().FindAsync(folder, cancellationToken);
}
