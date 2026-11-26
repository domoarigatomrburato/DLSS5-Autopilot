using Autopilot.Core.Executables;

namespace Autopilot.Core.Tests.Executables;

[TestClass]
public sealed class AgilityTests(TestContext testContext) : ExecutableTestBase(testContext)
{
    private string SystemFolder => In("System32");

    [TestMethod]
    public async Task Age_of_empires_iii_asking_for_version_4_on_a_windows_with_3_is_unmet()
    {
        string exe = Write(Requesting(4, ".\\"), Path.Join("AoE3DE", "AoE3DE_s.exe"));
        WriteSystemCore(3);

        AgilityUnmet? unmet = await Executable.CheckAgilityAsync(exe, SystemFolder, Ct);

        Assert.IsNotNull(unmet);
        Assert.AreEqual(new AgilityUnmet(exe, 4, ".\\", 3), unmet);
        Assert.AreEqual(
            "AoE3DE_s.exe asks Windows for the Direct3D 12 Agility SDK version 4 at '.\\', which it does not ship, and this Windows has version 3.",
            unmet.Explain());
    }

    [TestMethod]
    public async Task A_windows_with_a_newer_runtime_meets_the_request()
    {
        string exe = Write(Requesting(4, ".\\"));
        WriteSystemCore(600);

        Assert.IsNull(await Executable.CheckAgilityAsync(exe, SystemFolder, Ct));
    }

    [TestMethod]
    [DataRow(".\\", "D3D12Core.dll")]
    [DataRow("\\D3D12\\", "D3D12/D3D12Core.dll")]
    [DataRow("", "D3D12Core.dll")]
    public async Task A_game_that_ships_its_runtime_at_the_path_meets_the_request(string exported, string shipped)
    {
        string exe = Write(Requesting(4, exported));
        TestFiles.Plant(In(shipped), [0]);
        WriteSystemCore(3);

        Assert.IsNull(await Executable.CheckAgilityAsync(exe, SystemFolder, Ct));
    }

    [TestMethod]
    public async Task An_executable_that_asks_for_nothing_is_met()
    {
        string exe = Write(new TestPe().WithImports("d3d12.dll"));
        WriteSystemCore(3);

        Assert.IsNull(await Executable.AgilityRequestAsync(exe, Ct));
        Assert.IsNull(await Executable.CheckAgilityAsync(exe, SystemFolder, Ct));
    }

    [TestMethod]
    public async Task A_path_that_leaves_the_game_folder_is_never_followed()
    {
        string exe = Write(Requesting(4, "..\\..\\x"), Path.Join("games", "a", "game.exe"));
        WriteSystemCore(3);

        Assert.IsNull(await Executable.CheckAgilityAsync(exe, SystemFolder, Ct));
    }

    [TestMethod]
    public async Task Without_a_system_runtime_the_request_counts_as_met()
    {
        string exe = Write(Requesting(4, ".\\"));

        Assert.IsNull(await Executable.CheckAgilityAsync(exe, SystemFolder, Ct));
    }

    [TestMethod]
    public async Task A_system_runtime_that_cannot_be_parsed_counts_as_met()
    {
        string exe = Write(Requesting(4, ".\\"));
        TestFiles.Plant(Path.Join(SystemFolder, "D3D12Core.dll"), "not a dll");

        Assert.IsNull(await Executable.CheckAgilityAsync(exe, SystemFolder, Ct));
    }

    [TestMethod]
    public async Task An_executable_that_is_not_a_pe_is_an_error_not_a_verdict()
    {
        string exe = Write("MZ but nothing else"u8.ToArray());
        WriteSystemCore(3);

        _ = await FailsAsync(PeFailure.NotExecutable, () => Executable.CheckAgilityAsync(exe, SystemFolder, Ct));
    }

    [TestMethod]
    [DataRow(TestPe.Amd64)]
    [DataRow(TestPe.I386)]
    public async Task The_path_is_read_through_its_pointer(ushort machine)
    {
        TestPe pe = new(machine);
        _ = pe.WithExports(("D3D12SDKVersion", TestPe.U32(610)), ("D3D12SDKPath", pe.PointerTo(".\\D3D12\\")));

        Assert.AreEqual(new AgilityRequest(610, ".\\D3D12\\"), await Executable.AgilityRequestAsync(Write(pe), Ct));
    }

    [TestMethod]
    public async Task A_path_stored_inline_is_read_as_it_stands()
    {
        TestPe pe = new TestPe().WithExports(("D3D12SDKVersion", TestPe.U32(4)), ("D3D12SDKPath", "D3D12\\\0"u8.ToArray()));

        Assert.AreEqual(new AgilityRequest(4, "D3D12\\"), await Executable.AgilityRequestAsync(Write(pe), Ct));
    }

    [TestMethod]
    public async Task An_unreadable_or_missing_path_is_empty()
    {
        TestPe pointerNowhere = new TestPe().WithExports(("D3D12SDKVersion", TestPe.U32(4)), ("D3D12SDKPath", TestPe.U64(0x7FFF_0000_0000)));
        TestPe noPath = new TestPe().WithExports(("D3D12SDKVersion", TestPe.U32(4)), ("Other", [1]));

        Assert.AreEqual(new AgilityRequest(4, ""), await Executable.AgilityRequestAsync(Write(pointerNowhere, "a.exe"), Ct));
        Assert.AreEqual(new AgilityRequest(4, ""), await Executable.AgilityRequestAsync(Write(noPath, "b.exe"), Ct));
    }

    [TestMethod]
    public async Task A_version_export_that_points_nowhere_is_no_request()
    {
        TestPe pe = new TestPe().WithExportTable([("D3D12SDKVersion", 0x9_0000)]);

        Assert.IsNull(await Executable.AgilityRequestAsync(Write(pe), Ct));
    }

    [TestMethod]
    public async Task A_version_export_with_an_ordinal_past_the_functions_is_no_request()
    {
        TestPe pe = new();
        _ = pe.WithExportTable([("D3D12SDKVersion", pe.Put(TestPe.U32(4)))], functionCount: 0);

        Assert.IsNull(await Executable.AgilityRequestAsync(Write(pe), Ct));
    }

    [TestMethod]
    public async Task A_name_count_past_the_end_of_its_section_ends_the_walk()
    {
        TestPe pe = new();
        _ = pe.WithExportTable([("D3D12SDKVersion", pe.Put(TestPe.U32(4)))], nameCount: 50_000);

        Assert.AreEqual(new AgilityRequest(4, ""), await Executable.AgilityRequestAsync(Write(pe), Ct));
    }

    private static TestPe Requesting(uint version, string path)
    {
        TestPe pe = new();
        return pe.WithExports(("D3D12SDKVersion", TestPe.U32(version)), ("D3D12SDKPath", pe.PointerTo(path)));
    }

    private void WriteSystemCore(uint version)
    {
        TestPe core = new TestPe().WithExports(("D3D12SDKVersion", TestPe.U32(version)), ("D3D12SDKPath", "\0"u8.ToArray()));
        _ = Write(core, Path.Join("System32", "D3D12Core.dll"));
    }
}
