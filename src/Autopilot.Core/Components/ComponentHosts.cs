using Autopilot.Core.Net;

namespace Autopilot.Core.Components;

/// <summary>The hosts the components area may reach, redirects included, as the network spec's table lists them.</summary>
internal static class ComponentHosts
{
    public static HostAllowlist Allowlist { get; } = new(
        "reshade.me",
        "api.github.com",
        "github.com",
        "codeload.github.com",
        "raw.githubusercontent.com",
        "release-assets.githubusercontent.com",
        "objects.githubusercontent.com");
}
