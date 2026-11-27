using System.Text.Json.Nodes;
using Autopilot.Core.Components;
using Autopilot.Core.Net;
using Autopilot.Core.Tests.Archives;

namespace Autopilot.Core.Tests.Components;

/// <summary>OptiScaler release answers and package archives for the tests.</summary>
internal static class OptiScalerFixtures
{
    public static string List(OptiScalerBuild build) => ComponentsBed.ApiList(build.Repository, OptiScalerSource.ListedReleases);

    public static string Latest(OptiScalerBuild build) => ComponentsBed.Api(build.Repository, "/latest");

    public static JsonObject Release(OptiScalerBuild build, string tag, int age, params string[] assets) =>
        ReleaseJsonText.Release(build.Repository, tag, age, assets);

    /// <summary>A newest release of Dagherbou's page that carries another program.</summary>
    public static JsonObject DagherbouTools() => Release(OptiScalerBuild.Default, "v0.3.0-tools", 0, "DLSSNR-Overlay-Tools.zip");

    /// <summary>The captured Dagherbou list with <see cref="DagherbouTools"/> on top.</summary>
    public static JsonArray DagherbouAfterTools()
    {
        JsonArray list = ReleaseJsonText.Captured("optiscaler-dagherbou-releases.json");
        list.Insert(0, DagherbouTools());
        return list;
    }

    /// <summary>How many OptiScaler log lines name <paramref name="text"/>.</summary>
    public static int LinesAbout(ComponentsBed bed, string text) => bed.OptiScalerLines.Count(line => line.Contains(text, StringComparison.Ordinal));

    /// <summary>A file of the unpacked package, by its path under <paramref name="folder"/>.</summary>
    public static bool Holds(string folder, params string[] path) => File.Exists(Path.Combine([folder, .. path]));

    /// <summary>jlrouzies' captured sums file, listing <paramref name="archive"/>'s digest under <paramref name="listedName"/>.</summary>
    public static string SumsFor(byte[] archive, string listedName) =>
        ReleaseFixtures.Fixture("optiscaler-jlrouzies-v0.8.93-SHA256SUMS.txt")
            .Replace("8003065D88813B342049B869E8C894F8771C58F3D1A630DDF42FABD5C9D2F5FC", Net.CacheFixtures.DigestOf(archive).ToUpperInvariant(), StringComparison.Ordinal)
            .Replace("OptiScaler-NR-v0.8.93.zip", listedName, StringComparison.Ordinal);

    public static void RemoveNewest(JsonArray releases, int count)
    {
        for (int i = 0; i < count; i++)
        {
            releases.RemoveAt(0);
        }
    }

    /// <summary>A resolved release of wilsjo2's build whose archive is <paramref name="name"/>, checked against <paramref name="verification"/>.</summary>
    public static OptiScalerRelease Resolved(string name, Verification verification) =>
        new(
            OptiScalerBuild.Wilsjo2,
            "v0.10.0",
            new(name, new(ComponentsBed.Download(OptiScalerBuild.Wilsjo2.Repository, "v0.10.0", name)), null),
            verification,
            new(ReleaseOrigin.Api, TimeSpan.Zero));

    /// <summary>A zip of the named members; a name ending in '/' is a folder entry.</summary>
    public static byte[] Zip(params string[] members) =>
        FetchFixtures.Archive(ArchiveFormat.Zip, [.. members.Select(member => member.EndsWith('/', StringComparison.Ordinal) ? new TestMember(member) : TestMember.Text(member, member))]);
}
