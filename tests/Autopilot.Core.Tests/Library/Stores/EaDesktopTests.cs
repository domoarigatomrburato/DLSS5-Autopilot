using System.Text.Json;
using Autopilot.Core.Library.Stores;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Tests.Library.Stores;

[TestClass]
public sealed class EaDesktopTests(TestContext testContext) : IDisposable
{
    // The cap on store files, 16 MiB.
    private const int MaxStoreFileBytes = 16 * 1024 * 1024;

    private readonly StoreBed _bed = new();

    private CancellationToken Ct => testContext.CancellationToken;

    public void Dispose() => _bed.Dispose();

    // Known answers from GameFinder's tests (GPL-3.0, values only): tests/GameFinder.StoreHandlers.EADesktop.Tests/CryptoTests.
    [TestMethod]
    [DataRow("allUsersGenericId", "530c11479fe252fc5aabc24935b9776d4900eb3ba58fdc271e0d6229413ad40e")]
    [DataRow("allUsersGenericIdIS", "84efc4b836119c20419398c3f3f2bcef6fc52f9d86c6e4e8756aec5a8279e492")]
    public void The_fixtures_sha3_matches_known_answers(string input, string expected) =>
        Assert.AreEqual(expected, Convert.ToHexStringLower(EaDesktopFile.Sha3(input)));

    [TestMethod]
    public void The_fixtures_hardware_hash_matches_a_known_answer() =>
        Assert.AreEqual("3f37d8cece4441299a6abbd8db2acc0102cfd398", EaDesktopFile.Sha1Hex("erri120"));

    [TestMethod]
    public async Task Installed_games_are_listed_by_folder_name_with_no_store_pick()
    {
        string titanfall = _bed.Game("E/Games/Titanfall2", "Titanfall2.exe");
        string apex = _bed.Game("M/Games/Apex", "EasyAntiCheat_launcher.exe");
        Plant(Installed("Origin.SFT.50.0000532", "titanfall-2", titanfall + Path.DirectorySeparatorChar), Installed("Origin.SFT.50.0000848", "apex-legends", apex));

        StoreGame[] games = await FindAsync();

        CollectionAssert.AreEquivalent(
            new[] { new StoreGame("Titanfall2", titanfall + Path.DirectorySeparatorChar, GameSource.Ea, null), new StoreGame("Apex", apex, GameSource.Ea, null) },
            games);
    }

    [TestMethod]
    public async Task Games_owned_but_not_installed_have_no_install_path_and_are_not_listed()
    {
        string folder = _bed.Folder("E/Games/Titanfall2");
        Plant(Installed("1", "titanfall-2", folder), Installed("2", "plants-vs-zombies-garden-warfare-2", ""), new Dictionary<string, object?> { ["softwareId"] = "3", ["baseSlug"] = "nfs" });

        StoreBed.AssertNames(await FindAsync(), "Titanfall2");
    }

    [TestMethod]
    [DataRow("", "slug")]
    [DataRow("id", "")]
    [DataRow(null, "slug")]
    [DataRow("id", null)]
    public async Task An_entry_without_its_software_id_or_slug_is_skipped(string? softwareId, string? slug)
    {
        Plant(Installed(softwareId, slug, _bed.Folder("E/Game")));

        Assert.IsEmpty(await FindAsync());
    }

    [TestMethod]
    public async Task An_install_path_that_is_not_a_folder_on_disk_is_not_listed()
    {
        string file = _bed.WriteFile("E/file.bin");
        Plant(Installed("1", "gone", _bed.PathOf("E/Gone")), Installed("2", "file", file), Installed("3", "relative", "Games/Relative"), Installed("4", "kept", _bed.Folder("E/Kept")));

        StoreBed.AssertNames(await FindAsync(), "Kept");
    }

    [TestMethod]
    public async Task Another_machines_file_gives_nothing_and_the_other_ea_games_stay()
    {
        string registered = _bed.Folder("D/Mass Effect");
        _ = _bed.Registry.Machine32(@"SOFTWARE\Electronic Arts\Mass Effect", "Install Dir", registered);
        TestFiles.Plant(_bed.PathOf(EaDesktopFile.RelativePath), EaDesktopFile.Encrypt(Body(Installed("1", "titanfall-2", _bed.Folder("E/Titanfall2"))), EaDesktopFile.MachineString + "x"));

        StoreBed.AssertNames(await FindAsync(), "Mass Effect");
    }

    [TestMethod]
    public async Task A_hardware_string_is_built_from_every_value_in_order()
    {
        HardwareIdentity swapped = EaDesktopFile.Machine with { ProcessorId = EaDesktopFile.Machine.ProcessorName, ProcessorName = EaDesktopFile.Machine.ProcessorId };
        Plant(Installed("1", "titanfall-2", _bed.Folder("E/Titanfall2")));

        Assert.IsEmpty(await FindAsync(swapped));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(10)]
    [DataRow(64)]
    [DataRow(64 + 15)]
    [DataRow(-1)]
    [DataRow(-16)]
    public async Task A_file_cut_short_gives_nothing(int length)
    {
        byte[] whole = EaDesktopFile.Encrypt(Body(Installed("1", "titanfall-2", _bed.Folder("E/Titanfall2"))));
        TestFiles.Plant(_bed.PathOf(EaDesktopFile.RelativePath), whole[..(length >= 0 ? length : whole.Length + length)]);

        Assert.IsEmpty(await FindAsync());
    }

    [TestMethod]
    [DataRow("not json")]
    [DataRow("[]")]
    [DataRow("{}")]
    [DataRow(/*lang=json,strict*/ "{\"installInfos\": {}}")]
    [DataRow(/*lang=json,strict*/ "{\"installInfos\": [1, \"x\", null, {\"baseInstallPath\": 5, \"softwareId\": \"1\", \"baseSlug\": \"s\"}]}")]
    [DataRow("{\"installInfos\": [")]
    public async Task A_file_that_is_not_the_expected_json_gives_nothing(string plain)
    {
        TestFiles.Plant(_bed.PathOf(EaDesktopFile.RelativePath), EaDesktopFile.Encrypt(plain));

        Assert.IsEmpty(await FindAsync());
    }

    [TestMethod]
    public async Task An_oversize_file_is_not_read()
    {
        string body = Body(Installed("1", "titanfall-2", _bed.Folder("E/Titanfall2")));
        TestFiles.Plant(_bed.PathOf(EaDesktopFile.RelativePath), EaDesktopFile.Encrypt(body + new string(' ', MaxStoreFileBytes)));

        Assert.IsEmpty(await FindAsync());
    }

    [TestMethod]
    public async Task No_hardware_identity_skips_ea_desktop_only()
    {
        string registered = _bed.Folder("D/Mass Effect");
        _ = _bed.Registry.Machine32(@"SOFTWARE\Electronic Arts\Mass Effect", "Install Dir", registered);
        Plant(Installed("1", "titanfall-2", _bed.Folder("E/Titanfall2")));

        StoreBed.AssertNames(await StoreBed.FindAsync(new EaSource(_bed.Registry, _bed.Folders, new FixedIdentity(null)), Ct), "Mass Effect");
        StoreBed.AssertNames(await StoreBed.FindAsync(new EaSource(_bed.Registry, _bed.Folders), Ct), "Mass Effect");
    }

    [TestMethod]
    public async Task No_program_data_folder_skips_ea_desktop()
    {
        string folder = _bed.Folder("Program Files/EA Games/Dead Space");
        Plant(Installed("1", "titanfall-2", _bed.Folder("E/Titanfall2")));
        HostFolders folders = _bed.Folders with { ProgramData = null };

        StoreGame game = (await StoreBed.FindAsync(new EaSource(_bed.Registry, folders, new FixedIdentity(EaDesktopFile.Machine)), Ct)).Single();

        Assert.AreEqual(folder, game.Folder);
    }

    [TestMethod]
    public async Task No_install_file_gives_the_other_ea_games()
    {
        _ = _bed.Folder("Program Files/EA Games/Dead Space");

        StoreBed.AssertNames(await FindAsync(), "Dead Space");
    }

    [TestMethod]
    public async Task A_game_in_the_install_file_and_the_registry_is_listed_once()
    {
        string folder = _bed.Folder("E/Games/Titanfall2");
        _ = _bed.Registry.Machine32(@"SOFTWARE\Respawn\Titanfall2", "Install Dir", folder);
        _ = _bed.Registry.Machine32(@"SOFTWARE\Electronic Arts\Titanfall2", "Install Dir", folder);
        Plant(Installed("1", "titanfall-2", folder + Path.DirectorySeparatorChar));

        StoreGame game = (await FindAsync()).Single();

        Assert.AreEqual("Titanfall2", game.Name);
    }

    private Task<StoreGame[]> FindAsync(HardwareIdentity? identity = null) =>
        StoreBed.FindAsync(new EaSource(_bed.Registry, _bed.Folders, new FixedIdentity(identity ?? EaDesktopFile.Machine)), Ct);

    private void Plant(params Dictionary<string, object?>[] installInfos) =>
        TestFiles.Plant(_bed.PathOf(EaDesktopFile.RelativePath), EaDesktopFile.Encrypt(Body(installInfos)));

    // The shape the EA app writes (schema 21): only the fields the reader looks at, plus some it ignores.
    private static string Body(params Dictionary<string, object?>[] installInfos) =>
        JsonSerializer.Serialize(new Dictionary<string, object> { ["installInfos"] = installInfos, ["schema"] = new Dictionary<string, int> { ["version"] = 21 } });

    private static Dictionary<string, object?> Installed(string? softwareId, string? baseSlug, string baseInstallPath) => new()
    {
        ["baseInstallPath"] = baseInstallPath,
        ["baseSlug"] = baseSlug,
        ["executableCheck"] = @"[HKEY_LOCAL_MACHINE\SOFTWARE\Respawn\Titanfall2\Install Dir]Titanfall2.exe",
        ["installCheck"] = @"[HKEY_LOCAL_MACHINE\SOFTWARE\Respawn\Titanfall2\Install Dir]__Installer\installerdata.xml",
        ["softwareId"] = softwareId,
    };

    private sealed class FixedIdentity(HardwareIdentity? identity) : IHardwareIdentity
    {
        public Task<HardwareIdentity?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(identity);
    }
}
