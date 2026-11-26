using System.Net.Sockets;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Net;

/// <summary>Each way a connection fails becomes the kind the person is told about.</summary>
[TestClass]
public sealed class FailureTests(TestContext testContext)
{
    private const string Url = "https://example.test/data.json";

    [TestMethod]
    [DataRow(SocketError.AccessDenied, NetErrorKind.Blocked)]
    [DataRow(SocketError.ConnectionRefused, NetErrorKind.Refused)]
    [DataRow(SocketError.TimedOut, NetErrorKind.Timeout)]
    [DataRow(SocketError.HostNotFound, NetErrorKind.Dns)]
    [DataRow(SocketError.TryAgain, NetErrorKind.Dns)]
    [DataRow(SocketError.NoData, NetErrorKind.Dns)]
    [DataRow(SocketError.ConnectionReset, NetErrorKind.Cut)]
    [DataRow(SocketError.ConnectionAborted, NetErrorKind.Cut)]
    [DataRow(SocketError.Shutdown, NetErrorKind.Cut)]
    public async Task A_connection_failure_is_named_by_its_socket_code(SocketError code, NetErrorKind kind)
    {
        NetException failure = await FailWith(RetryTests.Transport(HttpRequestError.ConnectionError, code));

        Assert.AreEqual(kind, failure.Kind);
        Assert.AreEqual("example.test", failure.Host);
    }

    [TestMethod]
    [DataRow(HttpRequestError.NameResolutionError, NetErrorKind.Dns)]
    [DataRow(HttpRequestError.ResponseEnded, NetErrorKind.Cut)]
    public async Task A_failure_is_named_by_its_http_error_code(HttpRequestError error, NetErrorKind kind)
    {
        NetException failure = await FailWith(new HttpRequestException(error, "failed"));

        Assert.AreEqual(kind, failure.Kind);
    }

    [TestMethod]
    [DataRow("[WinError 10013] In Zugriffsversuch", NetErrorKind.Blocked)]
    [DataRow("No such host is known (11001)", NetErrorKind.Dns)]
    [DataRow("Connection refused (10061)", NetErrorKind.Refused)]
    [DataRow("Operation timed out", NetErrorKind.Timeout)]
    [DataRow("something nobody expected", NetErrorKind.Cut)]
    public async Task A_failure_without_a_code_is_named_by_its_text(string message, NetErrorKind kind)
    {
        NetException failure = await FailWith(new HttpRequestException(HttpRequestError.Unknown, message));

        Assert.AreEqual(kind, failure.Kind);
    }

    [TestMethod]
    [DataRow("The remote certificate is invalid according to the validation procedure: RemoteCertificateNameMismatch", CertificateProblem.NameMismatch)]
    [DataRow("The remote certificate is invalid because of errors in the certificate chain: NotTimeValid", CertificateProblem.Clock)]
    [DataRow("The remote certificate is invalid because of errors in the certificate chain: UntrustedRoot", CertificateProblem.Interception)]
    [DataRow("The remote certificate is invalid because of errors in the certificate chain: PartialChain", CertificateProblem.MissingRoot)]
    [DataRow("The remote certificate was rejected by the provided RemoteCertificateValidationCallback.", CertificateProblem.Unknown)]
    public async Task A_refused_certificate_is_named_by_its_reason(string message, CertificateProblem problem)
    {
        NetException failure = await FailWith(RetryTests.Certificate(message));

        Assert.AreEqual(NetErrorKind.Certificate, failure.Kind);
        Assert.AreEqual(problem, failure.Certificate);
    }

    [TestMethod]
    public async Task A_handshake_cut_short_is_a_cut_connection_not_a_certificate()
    {
        NetException failure = await FailWith(
            new HttpRequestException(HttpRequestError.SecureConnectionError, "The SSL connection could not be established", new IOException("Received an unexpected EOF")));

        Assert.AreEqual(NetErrorKind.Cut, failure.Kind);
    }

    [TestMethod]
    public async Task A_connection_dropped_mid_body_is_cut()
    {
        using TestBed bed = new(testContext, TestBed.SingleAttempt);
        bed.Server.BreakBody(Url);

        NetException failure = await bed.StreamFails(Url);

        Assert.AreEqual(NetErrorKind.Cut, failure.Kind);
    }

    private async Task<NetException> FailWith(Exception thrown)
    {
        using TestBed bed = new(testContext, TestBed.SingleAttempt);
        bed.Server.Throw(Url, thrown);
        return await bed.GetTextFails(Url);
    }
}
