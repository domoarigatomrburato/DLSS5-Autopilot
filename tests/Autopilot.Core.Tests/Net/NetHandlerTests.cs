using System.Net;
using Autopilot.Core.Net;

namespace Autopilot.Core.Tests.Net;

[TestClass]
public sealed class NetHandlerTests
{
    [TestMethod]
    public void The_production_handler_leaves_redirects_cookies_and_certificates_to_the_tool_and_the_system()
    {
        using SocketsHttpHandler handler = NetHandler.Create(new NetOptions("1.2.3"));

        Assert.IsFalse(handler.AllowAutoRedirect, "redirects are checked hop by hop by the client");
        Assert.AreEqual(TimeSpan.FromSeconds(15), handler.ConnectTimeout);
        Assert.AreEqual(DecompressionMethods.None, handler.AutomaticDecompression, "sizes and resume offsets are the file's own bytes");
        Assert.IsFalse(handler.UseCookies);
        Assert.IsNull(handler.SslOptions.RemoteCertificateValidationCallback, "TLS is verified against the system store only");
        Assert.IsNull(handler.SslOptions.CertificateChainPolicy);
    }

    [TestMethod]
    public void The_connect_timeout_comes_from_the_options()
    {
        using SocketsHttpHandler handler = NetHandler.Create(new NetOptions("1.2.3") { ConnectTimeout = TimeSpan.FromSeconds(5) });

        Assert.AreEqual(TimeSpan.FromSeconds(5), handler.ConnectTimeout);
    }
}
