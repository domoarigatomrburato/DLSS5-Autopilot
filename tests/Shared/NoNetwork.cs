using System.Net;

namespace Autopilot.Tests;

/// <summary>
/// Unit tests run offline. Every default <see cref="HttpClient"/> goes through a proxy nobody
/// listens on, so a stray request fails instead of reaching a real server. A backstop only: code
/// that talks to the network takes its <see cref="HttpMessageHandler"/> from the caller.
/// </summary>
[TestClass]
public sealed class NoNetwork
{
    [AssemblyInitialize]
    public static void BlockNetwork(TestContext context) =>
        HttpClient.DefaultProxy = new WebProxy("http://127.0.0.1:1");
}
