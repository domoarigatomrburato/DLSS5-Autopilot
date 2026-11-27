using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Autopilot.Core.Platform;
using Autopilot.Core.State;
using Autopilot.Core.Tests.TestBeds;

namespace Autopilot.Core.Tests.State;

/// <summary>A per-game state store on a throwaway data folder, with one game, a clock whose waits are instant, and a drive that can fill up.</summary>
internal sealed class StateBed : IDisposable
{
    private readonly GameTree _data = new("invidia-claritatis");
    private readonly GameTree _game = new("Some Game");

    public StateBed()
    {
        Game = GameIdentity.Of(_game.Root);
        Store = NewStore();
    }

    /// <summary>The tool's data folder.</summary>
    public string Root => _data.Root;

    public RecordingTimers Time { get; } = new();

    public GameIdentity Game { get; }

    public GameStateStore Store { get; }

    /// <summary>The game's file, <c>games\&lt;key&gt;.json</c>.</summary>
    public string File => Path.Join(Root, "games", Game.Key + ".json");

    /// <summary>While set, every write finds the drive full.</summary>
    public bool DriveFull { get; set; }

    /// <summary>Runs as each write opens its temporary file.</summary>
    public Action? DuringWrite { get; set; }

    /// <summary>While set, opening a game's mutex is refused, as an elevated CLI's mutex refuses the app.</summary>
    public bool MutexRefused { get; set; }

    /// <summary>Another store on the same data folder, as another process (or a second instance) has.</summary>
    public GameStateStore NewStore() => new(
        Root,
        Time,
        file =>
        {
            DuringWrite?.Invoke();
            return new FullDriveStream(file, () => DriveFull, FullDriveStream.HostError);
        },
        name => MutexRefused ? throw new UnauthorizedAccessException($"Access to the mutex {name} is denied.") : NamedMutex.Open(name));

    public void Plant(string json) => TestFiles.Plant(File, json);

    public JsonObject OnDisk() => (JsonObject)JsonNode.Parse(System.IO.File.ReadAllText(File))!;

    public void Set(string key, int value, CancellationToken cancellation) => Store.Update(Game, state => state.Set(key, value, StateJson.Default.Int32), cancellation);

    public int Get(string key) => Store.Read(Game).Get(key, StateJson.Default.Int32);

    public StoreNoticeKind[] Notices() => [.. Store.TakeNotices().Select(notice => notice.Kind)];

    public void Dispose()
    {
        _data.Dispose();
        _game.Dispose();
    }
}

[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(string[]))]
internal sealed partial class StateJson : JsonSerializerContext;
