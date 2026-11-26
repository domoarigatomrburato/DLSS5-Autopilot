using Autopilot.Core.Executables;

namespace Autopilot.Core.Tests.Executables;

[TestClass]
public sealed class ArchitectureTests(TestContext testContext) : ExecutableTestBase(testContext)
{
    [TestMethod]
    [DataRow(TestPe.Amd64, Bitness.Bits64)]
    [DataRow(TestPe.I386, Bitness.Bits32)]
    public async Task An_x64_or_x86_executable_reads_as_64_or_32_bit(ushort machine, Bitness expected)
    {
        ReadableExecutable reading = await ReadableAsync(new TestPe(machine));

        Assert.AreEqual(expected, reading.Bitness);
        Assert.IsEmpty(reading.Imports);
        Assert.IsEmpty(reading.DelayImports);
    }

    [TestMethod]
    [DataRow(TestPe.Arm64, "ARM64")]
    [DataRow(TestPe.Arm64Ec, "ARM64EC")]
    [DataRow((ushort)0x01C4, "0x01C4")]
    public async Task An_executable_for_another_processor_is_unsupported_and_named(ushort machine, string named)
    {
        string path = Write(new TestPe(machine));

        PeException failure = await FailsAsync(PeFailure.UnsupportedMachine, () => Executable.ReadAsync(path, Ct));

        Assert.AreEqual(machine, failure.Machine);
        Assert.AreEqual(path, failure.Path);
        StringAssert.Contains(failure.Message, $"game.exe is built for an unsupported machine type: {named}.", StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("<!DOCTYPE html><html><body>not here</body></html>")]
    [DataRow("MZ")]
    public async Task A_file_without_a_dos_header_is_not_an_executable(string content)
    {
        string path = Write(System.Text.Encoding.ASCII.GetBytes(content));

        PeException failure = await FailsAsync(PeFailure.NotExecutable, () => Executable.ReadAsync(path, Ct));

        Assert.AreEqual("game.exe is not a Windows executable (no MZ signature).", failure.Message);
    }

    [TestMethod]
    [DataRow(0x40u)]
    [DataRow(0x7FFF_FFF0u)]
    public async Task A_dos_header_that_points_at_no_pe_signature_has_no_pe_header(uint newHeader)
    {
        byte[] bytes = new byte[0x80];
        "MZ"u8.CopyTo(bytes);
        TestPe.U32(newHeader).CopyTo(bytes, 0x3C);
        "NE\0\0"u8.CopyTo(bytes.AsSpan(0x40));
        string path = Write(bytes);

        PeException failure = await FailsAsync(PeFailure.NoPeHeader, () => Executable.ReadAsync(path, Ct));

        Assert.AreEqual("game.exe has no PE header.", failure.Message);
    }

    [TestMethod]
    [DataRow(TestPe.Amd64, (ushort)0x10B)]
    [DataRow(TestPe.I386, (ushort)0x20B)]
    public async Task An_optional_header_that_disagrees_with_the_machine_is_corrupt(ushort machine, ushort magic)
    {
        string path = Write(new TestPe(machine) { Magic = magic });

        _ = await FailsAsync(PeFailure.CorruptHeader, () => Executable.ReadAsync(path, Ct));
    }

    [TestMethod]
    [DataRow(0x46)]
    [DataRow(0x100)]
    public async Task Headers_cut_short_after_the_machine_are_corrupt(int keep)
    {
        string path = Write(new TestPe().ToBytes()[..keep]);

        PeException failure = await FailsAsync(PeFailure.CorruptHeader, () => Executable.ReadAsync(path, Ct));

        Assert.AreEqual("game.exe has a damaged PE header.", failure.Message);
    }

    [TestMethod]
    public async Task A_missing_file_cannot_be_read()
    {
        string path = In("gone.exe");

        PeException failure = await FailsAsync(PeFailure.Unreadable, () => Executable.ReadAsync(path, Ct));

        StringAssert.StartsWith(failure.Message, "Cannot read gone.exe: ", StringComparison.Ordinal);
        Assert.IsInstanceOfType<FileNotFoundException>(failure.InnerException);
    }

    [TestMethod]
    public async Task A_folder_is_unreadable_not_protected()
    {
        string path = In("folder.exe");
        _ = Directory.CreateDirectory(path);

        _ = await FailsAsync(PeFailure.Unreadable, () => Executable.ReadAsync(path, Ct));
    }
}
