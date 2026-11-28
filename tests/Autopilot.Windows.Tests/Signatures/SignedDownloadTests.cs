using System.Net;
using Autopilot.Core.Net;
using Autopilot.Windows.Signatures;
using Microsoft.Extensions.Logging.Abstractions;

namespace Autopilot.Windows.Tests.Signatures;

/// <summary>The download cache with the real signature check: the signer must be the publisher. Runs on Windows only.</summary>
[TestClass]
public sealed class SignedDownloadTests(TestContext testContext)
{
    [TestMethod]
    public async Task A_file_microsoft_signed_is_refused_where_nvidias_signature_is_expected()
    {
        using DownloadBed bed = new(await File.ReadAllBytesAsync(SignedFiles.MicrosoftSigned, testContext.CancellationToken));

        NetException failure = await Assert.ThrowsAsync<NetException>(() => bed.Download(new SignerVerification("NVIDIA Corporation"), testContext.CancellationToken));

        Assert.AreEqual(new VerificationFailedDetail(VerificationProblem.WrongSigner, DownloadBed.Name), failure.Detail);
        Assert.Contains("Microsoft Corporation", failure.Message);
    }

    /// <summary>A download cache in a throwaway folder, whose server answers every request with one file.</summary>
    private sealed class DownloadBed : IDisposable
    {
        public const string Name = "nvngx_dlss.dll";

        private static readonly Uri _url = new("https://example.test/" + Name);

        private readonly string _cacheRoot = Directory.CreateTempSubdirectory("autopilot-signed-download-").FullName;
        private readonly OneFile _server;
        private readonly NetClient _client;
        private readonly DownloadCache _cache;

        public DownloadBed(byte[] file)
        {
            _server = new(file);
            _client = new(_server, new NetOptions("1.2.3"), TimeProvider.System, _cacheRoot, NullLogger<NetClient>.Instance);
            _cache = new(_client, _cacheRoot, new AuthenticodeVerifier(), TimeProvider.System, NullLogger<DownloadCache>.Instance);
        }

        public Task<CachedFile> Download(Verification verification, CancellationToken cancellationToken) =>
            _cache.DownloadAsync(_url, new HostAllowlist(_url.Host), Name, verification, null, null, cancellationToken);

        public void Dispose()
        {
            _client.Dispose();
            _server.Dispose();
            Directory.Delete(_cacheRoot, recursive: true);
        }
    }

    private sealed class OneFile(byte[] body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body), RequestMessage = request });
    }
}
