using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

namespace Autopilot.Windows.Tests.Signatures;

/// <summary>The files the signature tests check, found or made at run time in a throwaway folder.</summary>
internal sealed class SignedFiles : IDisposable
{
    public string Folder { get; } = Directory.CreateTempSubdirectory("autopilot-signatures-").FullName;

    /// <summary>
    /// The <c>dotnet.exe</c> running the tests, embedded-signed by Microsoft. Under Microsoft.Testing.Platform
    /// the process is the test project's own executable; <c>dotnet test</c> names its host in
    /// <c>DOTNET_HOST_PATH</c>. Run directly, the host is the folder three levels above the runtime's.
    /// </summary>
    public static string MicrosoftSigned
    {
        get
        {
            string? host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            string path = string.IsNullOrEmpty(host)
                ? Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", "dotnet.exe"))
                : host;
            Assert.IsTrue(File.Exists(path), $"no dotnet.exe at {path}");
            return path;
        }
    }

    /// <summary>Our own build output, which nobody signs.</summary>
    public static string Unsigned => typeof(Core.Net.ISignatureVerifier).Assembly.Location;

    /// <summary><paramref name="source"/> copied into the folder as <paramref name="name"/>.</summary>
    public string Copy(string source, string name)
    {
        string copy = Path.Combine(Folder, name);
        File.Copy(source, copy);
        return copy;
    }

    /// <summary>A copy of a signed PE with one byte flipped halfway to its certificate table, inside the signed range.</summary>
    public string Tampered(string source, string name)
    {
        byte[] bytes = File.ReadAllBytes(source);
        int certificates = CertificateTableOffset(bytes);
        Assert.IsGreaterThan(0, certificates, "the source carries an embedded signature");
        bytes[certificates / 2] ^= 0xFF;
        string copy = Path.Combine(Folder, name);
        File.WriteAllBytes(copy, bytes);
        return copy;
    }

    public string Text(string name, string content)
    {
        string path = Path.Combine(Folder, name);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose() => Directory.Delete(Folder, recursive: true);

    // The certificate table's directory holds a file offset, not a virtual address.
    private static int CertificateTableOffset(byte[] pe)
    {
        using MemoryStream stream = new(pe, writable: false);
        return new PEHeaders(stream).PEHeader?.CertificateTableDirectory.RelativeVirtualAddress ?? 0;
    }
}
