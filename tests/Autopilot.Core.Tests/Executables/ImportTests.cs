using System.Text;
using Autopilot.Core.Executables;

namespace Autopilot.Core.Tests.Executables;

[TestClass]
public sealed class ImportTests(TestContext testContext) : ExecutableTestBase(testContext)
{
    [TestMethod]
    [DataRow(TestPe.Amd64)]
    [DataRow(TestPe.I386)]
    public async Task Static_and_delay_load_imports_are_read_in_both_bitnesses(ushort machine)
    {
        TestPe pe = new TestPe(machine).WithImports("KERNEL32.dll", "d3d9.dll").WithDelayImports("D3D12.dll", "xinput1_3.dll");

        ReadableExecutable reading = await ReadableAsync(pe);

        AssertNames(reading.Imports, "kernel32.dll", "d3d9.dll");
        AssertNames(reading.DelayImports, "d3d12.dll", "xinput1_3.dll");
    }

    [TestMethod]
    public async Task Imports_match_whole_names_ignoring_case()
    {
        ReadableExecutable reading = await ReadableAsync(new TestPe().WithImports("xd3d11.dll", "D3D9.DLL"));

        Assert.DoesNotContain("d3d11.dll", reading.Imports);
        Assert.Contains("d3d9.dll", reading.Imports);
        Assert.Contains("D3D9.dll", reading.Imports);
    }

    [TestMethod]
    public async Task A_dll_named_only_in_the_bytes_is_not_an_import()
    {
        TestPe pe = new TestPe().WithImports("d3d9.dll").Append("d3d11.dll", Encoding.Latin1).Append("d3d12.dll", Encoding.Unicode);

        ReadableExecutable reading = await ReadableAsync(pe);

        AssertNames(reading.Imports, "d3d9.dll");
        Assert.IsEmpty(reading.DelayImports);
    }

    [TestMethod]
    public async Task A_delay_load_descriptor_of_virtual_addresses_is_skipped()
    {
        TestPe pe = new();
        _ = pe.DelayTable((0, pe.PutAscii("d3d9.dll")), (1, pe.PutAscii("d3d11.dll")), (0, 0));

        ReadableExecutable reading = await ReadableAsync(pe);

        AssertNames(reading.DelayImports, "d3d11.dll");
    }

    [TestMethod]
    public async Task A_name_outside_every_section_is_skipped()
    {
        TestPe pe = new();
        _ = pe.ImportTable(0x9_0000, pe.PutAscii("d3d11.dll"), 0);

        ReadableExecutable reading = await ReadableAsync(pe);

        AssertNames(reading.Imports, "d3d11.dll");
    }

    [TestMethod]
    public async Task A_name_in_a_section_beyond_its_raw_data_is_skipped()
    {
        TestPe pe = new();
        uint table = pe.ImportTable(pe.PutAscii("d3d11.dll"), 1, 0);
        pe.EndRawData();
        pe.Patch(table + TestPe.ImportDescriptorSize + 12, pe.PutAscii("d3d9.dll"));

        ReadableExecutable reading = await ReadableAsync(pe);

        AssertNames(reading.Imports, "d3d11.dll");
    }

    [TestMethod]
    public async Task Descriptors_after_the_terminator_are_not_read()
    {
        TestPe pe = new();
        _ = pe.ImportTable(pe.PutAscii("d3d11.dll"), 0, pe.PutAscii("d3d9.dll"), 0);

        ReadableExecutable reading = await ReadableAsync(pe);

        AssertNames(reading.Imports, "d3d11.dll");
    }

    [TestMethod]
    public async Task At_most_1024_descriptors_are_read()
    {
        TestPe pe = new();
        _ = pe.ImportTable([.. Enumerable.Range(0, 1100).Select(i => pe.PutAscii($"lib{i:D4}.dll"))]);

        ReadableExecutable reading = await ReadableAsync(pe);

        Assert.HasCount(1024, reading.Imports);
        Assert.Contains("lib1023.dll", reading.Imports);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(10)]
    public async Task A_table_cut_short_by_its_section_keeps_the_names_before_the_cut(int cut)
    {
        TestPe pe = new();
        _ = pe.ImportTable(pe.PutAscii("d3d11.dll"), pe.PutAscii("dxgi.dll"));
        pe.EndRawData(cut);

        ReadableExecutable reading = await ReadableAsync(pe);

        AssertNames(reading.Imports, cut == 0 ? ["d3d11.dll", "dxgi.dll"] : ["d3d11.dll"]);
    }

    [TestMethod]
    [DataRow(1u, 0, 0)]
    [DataRow(2u, 1, 0)]
    [DataRow(13u, 1, 0)]
    [DataRow(14u, 1, 1)]
    public async Task A_directory_count_at_or_below_a_table_means_no_table(uint count, int imports, int delayImports)
    {
        TestPe pe = new TestPe { DirectoryCount = count }.WithImports("d3d11.dll").WithDelayImports("d3d12.dll");

        ReadableExecutable reading = await ReadableAsync(pe);

        Assert.HasCount(imports, reading.Imports);
        Assert.HasCount(delayImports, reading.DelayImports);
    }

    [TestMethod]
    public async Task A_name_longer_than_256_bytes_or_not_ascii_is_skipped()
    {
        TestPe pe = new();
        _ = pe.ImportTable(pe.PutAscii(new string('a', 300) + ".dll"), pe.Put("d3dé.dll\0"u8), pe.PutAscii("vulkan-1.dll"), 0);

        ReadableExecutable reading = await ReadableAsync(pe);

        AssertNames(reading.Imports, "vulkan-1.dll");
    }

    [TestMethod]
    [Timeout(10_000)] // A 500 MB file is created first; the read itself must take far less.
    public async Task A_500_mb_executable_with_its_imports_at_the_end_is_read_by_range()
    {
        const long size = 500L << 20;
        TestPe pe = new TestPe { RawOffset = size - 0x10000, Size = size }.WithImports("d3d12.dll");
        string path = Write(pe);
        Assert.AreEqual(size, new FileInfo(path).Length);
        TimeProvider clock = TimeProvider.System;
        long started = clock.GetTimestamp();

        ExecutableReading reading = await Executable.ReadAsync(path, Ct);

        AssertNames(Assert.IsInstanceOfType<ReadableExecutable>(reading).Imports, "d3d12.dll");
        Assert.IsLessThan(TimeSpan.FromSeconds(1), clock.GetElapsedTime(started));
    }
}
