using System.Text;
using Autopilot.Core.Components;
using Autopilot.Core.Tests.Executables;

namespace Autopilot.Core.Tests.Components;

[TestClass]
public sealed class NvidiaFileTests(TestContext testContext)
{
    private readonly List<string> _directories = [];

    [TestCleanup]
    public void DeleteDirectories()
    {
        foreach (string directory in _directories)
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task A_64_bit_dll_of_200_000_bytes_or_more_passes() =>
        Assert.IsNull(await Check(new TestPe(TestPe.Amd64) { Size = NvidiaFiles.MinRawSize }));

    [TestMethod]
    public async Task A_small_file_is_not_a_runtime()
    {
        RawDllRejection? rejection = await Check(new TestPe(TestPe.Amd64));

        Assert.AreEqual(RawDllProblem.TooSmall, rejection?.Problem);
        StringAssert.Contains(rejection!.Message, "200,000 bytes or more", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task A_32_bit_dll_is_refused()
    {
        RawDllRejection? rejection = await Check(new TestPe(TestPe.I386) { Size = 300_000 });

        Assert.AreEqual(RawDllProblem.Not64Bit, rejection?.Problem);
        Assert.AreEqual("nvngx_dlss.dll is a 32-bit DLL; NVIDIA's runtime is a 64-bit (x64) one.", rejection!.Message);
    }

    [TestMethod]
    public async Task Another_machine_is_refused()
    {
        RawDllRejection? rejection = await Check(new TestPe(TestPe.Arm64) { Size = 300_000 });

        Assert.AreEqual(RawDllProblem.NotExecutable, rejection?.Problem);
        StringAssert.Contains(rejection!.Message, "ARM64", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task A_page_answered_in_place_of_the_dll_is_refused()
    {
        string path = Write(Encoding.UTF8.GetBytes("<!DOCTYPE html>" + new string(' ', 250_000)));

        RawDllRejection? rejection = await NvidiaFiles.CheckRawAsync(path, testContext.CancellationToken);

        Assert.AreEqual(RawDllProblem.NotExecutable, rejection?.Problem);
    }

    private async Task<RawDllRejection?> Check(TestPe pe) => await NvidiaFiles.CheckRawAsync(Write(pe.ToBytes()), testContext.CancellationToken);

    private string Write(byte[] content)
    {
        string directory = Directory.CreateTempSubdirectory("autopilot-nvidia-").FullName;
        string path = Path.Join(directory, "nvngx_dlss.dll");
        File.WriteAllBytes(path, content);
        _directories.Add(directory);
        return path;
    }
}
