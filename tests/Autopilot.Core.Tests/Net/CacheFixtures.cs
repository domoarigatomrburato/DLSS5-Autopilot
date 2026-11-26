using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Net;

/// <summary>What the cache tests download: a small real zip, and the HTML page a filter answers with instead.</summary>
internal static class CacheFixtures
{
    public static byte[] Zip { get; } = MakeZip();

    public static string ZipDigest { get; } = DigestOf(Zip);

    public static Sha256Verification ZipVerification { get; } = new(ZipDigest);

    public static byte[] Page { get; } = Encoding.UTF8.GetBytes("<!DOCTYPE html><html><body>Blocked by your network</body></html>");

    public static string DigestOf(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));

    private static byte[] MakeZip()
    {
        using MemoryStream buffer = new();
        using (ZipArchive archive = new(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            using StreamWriter readme = new(archive.CreateEntry("tool/readme.txt").Open());
            readme.Write("hello");
        }
        return buffer.ToArray();
    }
}

/// <summary>Answers each signature check from a queue; the last answer repeats.</summary>
internal sealed class FakeSigner : ISignatureVerifier
{
    private readonly Queue<bool> _answers = new([true]);

    public List<string> Checked { get; } = [];

    public void Answer(params bool[] answers)
    {
        _answers.Clear();
        foreach (bool answer in answers)
        {
            _answers.Enqueue(answer);
        }
    }

    public Task<bool> IsSignedByAsync(string path, string publisher, CancellationToken cancellationToken)
    {
        Checked.Add(path);
        return Task.FromResult(_answers.Count > 1 ? _answers.Dequeue() : _answers.Peek());
    }
}

/// <summary>A progress sink that fails the way a write to a full drive does, after the first chunk was written.</summary>
internal sealed class FullDrive(Exception failure) : IProgress<DownloadProgress>
{
    public void Report(DownloadProgress value) => throw failure;
}
