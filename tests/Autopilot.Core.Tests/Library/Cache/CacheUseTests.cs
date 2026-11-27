using System.Text.Json.Nodes;
using Autopilot.Core.Library;
using Autopilot.Core.Library.Stores;

namespace Autopilot.Core.Tests.Library.Cache;

/// <summary>When a saved library is used at all: the same tool, the same GPU, a file that reads as one.</summary>
[TestClass]
public sealed class CacheUseTests(TestContext testContext) : IDisposable
{
    private readonly CacheBed _bed = new();

    private CancellationToken Ct => testContext.CancellationToken;

    public void Dispose() => _bed.Dispose();

    [TestMethod]
    public async Task A_saved_library_comes_back_as_it_was_saved()
    {
        _ = _bed.Game("Arena");
        _ = _bed.Games.Exe("Arena/Tools/Editor.exe");
        _ = _bed.Games.Exe("Racer/bin/Racer.exe", imports: "d3d12.dll");
        _ = _bed.Games.Folder("Empty");
        GameEntry[] saved =
        [
            await _bed.ReadAsync("Arena", Ct),
            await _bed.ReadAsync("Racer", Ct, GameSource.Epic, "Racer/bin/Racer.exe"),
            await _bed.ReadAsync("Empty", Ct, GameSource.Gog),
        ];

        Assert.IsTrue((await _bed.SaveAsync(Ct, saved)).Saved);
        CachedLibrary loaded = await _bed.LoadedAsync(Ct);

        CacheBed.AssertSameLibrary(saved, loaded.Games);

        Assert.IsEmpty(loaded.ReadAgain);
        Assert.IsEmpty(loaded.Notes);
    }

    [TestMethod]
    public async Task A_library_saved_with_a_byte_order_mark_is_used()
    {
        _ = _bed.Game("Arena");
        GameEntry saved = await _bed.ReadAsync("Arena", Ct);
        _ = await _bed.SaveAsync(Ct, saved);
        TestFiles.AddByteOrderMark(_bed.CachePath);

        CacheBed.AssertSameLibrary([saved], (await _bed.LoadedAsync(Ct)).Games);
    }

    [TestMethod]
    [DataRow("2.0.1", 89, DisplayName = "another tool version")]
    [DataRow("2.0.0", 120, DisplayName = "another GPU")]
    [DataRow("2.0.0", null, DisplayName = "no GPU read")]
    public async Task A_library_saved_by_another_tool_version_or_for_another_GPU_is_not_used(string version, int? sm)
    {
        _ = _bed.Game("Arena");
        _ = await _bed.SaveAsync(Ct, await _bed.ReadAsync("Arena", Ct));

        Assert.IsNull(await _bed.LoadAsync(Ct, new CacheKey(version, sm)));
    }

    [TestMethod]
    [DataRow(2, DisplayName = "an older schema")]
    [DataRow(4, DisplayName = "a newer schema")]
    public async Task A_file_of_another_schema_is_not_used(int schema)
    {
        _ = _bed.Game("Arena");
        _ = await _bed.SaveAsync(Ct, await _bed.ReadAsync("Arena", Ct));

        _bed.Edit(file => file["schema"] = schema);

        Assert.IsNull(await _bed.LoadAsync(Ct));
    }

    [TestMethod]
    [DataRow("{\"schema\": 3, \"toolVersion\": \"2.0.0\", \"gpuSm\": 89, \"games\": [", DisplayName = "cut short")]
    [DataRow("not json at all", DisplayName = "not JSON")]
    [DataRow("[]", DisplayName = "a list")]
    [DataRow("{}", DisplayName = "an empty object")]
    [DataRow(/*lang=json,strict*/ "{\"schema\": 3, \"toolVersion\": \"2.0.0\", \"gpuSm\": 89, \"games\": [null]}", DisplayName = "a null game")]
    public async Task A_file_that_is_not_a_library_is_not_used(string text)
    {
        TestFiles.Plant(_bed.CachePath, text);

        Assert.IsNull(await _bed.LoadAsync(Ct));
    }

    [TestMethod]
    [DataRow("source", 0, DisplayName = "an enum as a number")]
    [DataRow("source", "Stream", DisplayName = "an enum name no source has")]
    [DataRow("folder", "Games/Arena", DisplayName = "a relative folder")]
    [DataRow("executable", "Arena.exe", DisplayName = "a relative executable")]
    [DataRow("name", null, DisplayName = "no name")]
    [DataRow("stamp", null, DisplayName = "no stamp")]
    public async Task A_game_of_the_wrong_shape_makes_the_file_unused(string field, object? value)
    {
        _ = _bed.Game("Arena");
        _ = await _bed.SaveAsync(Ct, await _bed.ReadAsync("Arena", Ct));

        JsonNode? node = value switch
        {
            int number => JsonValue.Create(number),
            string text => JsonValue.Create(text),
            _ => null,
        };
        _bed.Edit(file => CacheBed.FirstGame(file)[field] = node);

        Assert.IsNull(await _bed.LoadAsync(Ct));
    }

    [TestMethod]
    public async Task A_relative_candidate_makes_the_file_unused()
    {
        _ = _bed.Game("Arena");
        _ = await _bed.SaveAsync(Ct, await _bed.ReadAsync("Arena", Ct));

        _bed.Edit(file => CacheBed.FirstGame(file)["candidates"] = new JsonArray("Arena.exe"));

        Assert.IsNull(await _bed.LoadAsync(Ct));
    }

    [TestMethod]
    public async Task A_file_over_64_MiB_is_not_read_even_when_it_parses()
    {
        _ = _bed.Game("Arena");
        _ = await _bed.SaveAsync(Ct, await _bed.ReadAsync("Arena", Ct));
        byte[] spaces = new byte[1024 * 1024];
        Array.Fill(spaces, (byte)' ');
        await using (FileStream file = new(_bed.CachePath, FileMode.Append))
        {
            for (int megabyte = 0; megabyte < 64; megabyte++)
            {
                await file.WriteAsync(spaces, Ct);
            }
        }

        Assert.IsNull(await _bed.LoadAsync(Ct));
    }

    [TestMethod]
    public async Task No_file_is_no_cache() => Assert.IsNull(await _bed.LoadAsync(Ct));

    [TestMethod]
    public async Task A_library_saved_with_no_games_is_no_cache()
    {
        Assert.IsTrue((await _bed.SaveAsync(Ct)).Saved);

        Assert.IsNull(await _bed.LoadAsync(Ct));
    }

    [TestMethod]
    public async Task Forgetting_the_cache_means_the_next_load_finds_none()
    {
        _ = _bed.Game("Arena");
        _ = await _bed.SaveAsync(Ct, await _bed.ReadAsync("Arena", Ct));

        LibraryCache.Forget(_bed.CachePath);
        LibraryCache.Forget(_bed.CachePath);

        Assert.IsNull(await _bed.LoadAsync(Ct));
    }
}
