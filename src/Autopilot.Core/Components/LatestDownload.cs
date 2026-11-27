using Autopilot.Core.Resources;

namespace Autopilot.Core.Components;

/// <summary>
/// The last resort for a component taken by asset name: GitHub's
/// <c>github.com/&lt;repo&gt;/releases/latest/download/&lt;asset&gt;</c> redirect. Its version is
/// <see cref="ComponentVersion.Latest"/>, which is never compared.
/// </summary>
public sealed record LatestDownload(Uri Url, ComponentVersion Version)
{
    /// <summary>The words the install shows: the exact version is whatever the redirect points at.</summary>
    public string Notice { get; } = Texts.Text("ReleasesFromLatestRedirect");
}
