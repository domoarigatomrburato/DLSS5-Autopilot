using System.Text.Json;
using System.Text.Json.Nodes;
using Autopilot.Core.Install;
using Autopilot.Core.Tests.TestBeds;
using Microsoft.Extensions.Logging.Testing;

namespace Autopilot.Core.Tests.Install;

/// <summary>A game folder with install's reader and writer, logging to a fake logger.</summary>
internal sealed class RecordBed(params IEnumerable<TreeFile> files) : IDisposable
{
    public GameTree Tree { get; } = GameTree.Of(files);

    public FakeLogger<InstallRecords> Log { get; } = new();

    public InstallRecords Records => Recorded.Reader(Log);

    /// <summary>The lines logged so far.</summary>
    public string[] LogLines => [.. Log.Collector.GetSnapshot().Select(record => record.Message)];

    /// <summary>The names of the files directly in the game folder, sorted.</summary>
    public string Files => string.Join(", ", Directory.GetFiles(Tree.Root).Select(Path.GetFileName).Order(StringComparer.Ordinal));

    /// <summary>The record file in <paramref name="installFolder"/>, relative to the game folder.</summary>
    public string RecordPath(string installFolder = "") => InstallRecords.PathOf(Tree.PathOf(installFolder));

    /// <summary>Writes these bytes as the record, as a crash, a person or another tool may leave it.</summary>
    public void PlantRaw(string text, string installFolder = "") => TestFiles.Plant(RecordPath(installFolder), text);

    /// <summary>Writes <paramref name="record"/> after <paramref name="change"/> edits its JSON.</summary>
    public void PlantEdited(InstallRecord record, Action<JsonObject> change, string installFolder = "")
    {
        JsonObject json = JsonSerializer.SerializeToNode(record, InstallJsonContext.Default.InstallRecord)!.AsObject();
        change(json);
        PlantRaw(json.ToJsonString(), installFolder);
    }

    /// <summary>The record file in the game folder's root, parsed.</summary>
    public JsonObject OnDisk() => JsonNode.Parse(File.ReadAllText(RecordPath()))!.AsObject();

    public Task SaveAsync(InstallRecord record, CancellationToken cancellationToken, string installFolder = "") =>
        Records.SaveAsync(Tree.PathOf(installFolder), record, cancellationToken);

    public Task<RecordedInstall?> FindAsync(string installFolder, CancellationToken cancellationToken) =>
        Records.FindAsync(Tree.PathOf(installFolder), cancellationToken);

    public void Dispose() => Tree.Dispose();
}

/// <summary>What a test expects the reader to find in a bed's folder.</summary>
internal static class RecordChecks
{
    /// <summary>The record found, which the test expects to be there.</summary>
    public static async Task<RecordedInstall> FoundAsync(this RecordBed bed, string installFolder, CancellationToken cancellationToken) =>
        await bed.FindAsync(installFolder, cancellationToken) ?? throw new AssertFailedException("no record found");

    /// <summary>The paths of the entries found, joined by <c>|</c>.</summary>
    public static async Task<string> EntryPathsAsync(this RecordBed bed, string installFolder, CancellationToken cancellationToken) =>
        string.Join("|", (await bed.FoundAsync(installFolder, cancellationToken)).Record!.Entries.Select(entry => entry.Path));

    /// <summary>Checks the folder's record is there and unreadable for <paramref name="problem"/>.</summary>
    public static async Task AssertUnreadableAsync(this RecordBed bed, RecordProblem problem, CancellationToken cancellationToken, string installFolder = "")
    {
        RecordedInstall found = await bed.FoundAsync(installFolder, cancellationToken);
        Assert.AreEqual(InstallState.Unreadable, found.State);
        Assert.AreEqual(problem, found.Problem);
        Assert.IsNull(found.Record);
        Assert.IsEmpty(found.Executables);
    }
}
