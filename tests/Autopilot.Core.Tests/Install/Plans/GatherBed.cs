using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.Mirror;
using Autopilot.Core.Install;
using Autopilot.Core.Install.Plans;
using Autopilot.Core.Routes;
using Autopilot.Core.Tests.Archives;
using Autopilot.Core.Tests.Components;
using Autopilot.Core.Tests.Components.Fetch;
using Autopilot.Core.Tests.Components.Mirror;
using Autopilot.Core.Tests.Executables;
using Autopilot.Core.Tests.TestBeds;
using Microsoft.Extensions.Logging.Abstractions;

namespace Autopilot.Core.Tests.Install.Plans;

/// <summary>
/// A gather on the network test bed: reshade.me's setup with its appended zip, the mirror's list and builds (no digests,
/// so only the signer, faked as NVIDIA's, checks their contents), and NVIDIA's own runtimes when the test serves them.
/// </summary>
internal sealed class GatherBed : IDisposable
{
    /// <summary>The tag of the pinned RTX 40 <c>nvngx_dlssnr.dll</c> build, whose pin lists sm 89 and 120.</summary>
    public const string RtxFortyTag = "dlssnr-310.8.0-RTX40";

    /// <summary>The setup reshade-home.html links.</summary>
    public const string Setup = "https://reshade.me/downloads/ReShade_Setup_6.8.0_Addon.exe";

    private readonly GameTree _own = new("own");

    // Whether NVIDIA's latest-release redirect has its answer: served, or failing once the first gather starts.
    private bool _nvidiaAnswered;

    public GatherBed(TestContext context)
    {
        Components = new(context);
        _ = Components.Fixture("https://reshade.me/", "reshade-home.html");
        _ = Components.Server.AnswerBytes(Setup, [.. new TestPe().ToBytes(), .. GatherFiles.Zip(("ReShade64.dll", GatherFiles.Dll("64")), ("ReShade32.dll", GatherFiles.Dll("32")), ("ReShade64.json", "{}"u8.ToArray()), ("ReShade32.json", "{}"u8.ToArray()))]);
        OwnAddon = Path.Join(_own.Root, "renodx-dlss5.addon64");
        File.WriteAllBytes(OwnAddon, GatherFiles.Dll("my renodx"));
    }

    public ComponentsBed Components { get; }

    /// <summary>The person's own <c>renodx-dlss5.addon64</c>, outside the game folder.</summary>
    public string OwnAddon { get; }

    /// <summary>The mirror lists these releases, newest first, each one zip asset holding <paramref name="releases"/>' member; NVIDIA's redirect fails unless served.</summary>
    public GatherBed Mirror(params (string Tag, string Member, byte[] Data)[] releases)
    {
        GatherFiles.ServeMirror(Components, releases);
        return this;
    }

    /// <summary>NVIDIA's latest release is <c>v310.9.16</c>, its raw <c>nvngx_dlss.dll</c> and <c>nvngx_dlssd.dll</c> served as <paramref name="dll"/>.</summary>
    public GatherBed Nvidia(byte[] dll)
    {
        GatherFiles.ServeNvidia(Components, dll);
        _nvidiaAnswered = true;
        return this;
    }

    /// <summary>
    /// The usual mirror: a dlssnr build for RTX 50, the pinned RTX 40 build (its file is not the pinned one, so it is never
    /// placed), one for RTX 30 and RTX 50, a <c>nvngx_dlss.dll</c> and a <c>nvngx_dlssd.dll</c>.
    /// </summary>
    public GatherBed UsualMirror() => Mirror(
        ("dlssnr-310.9.16-nvidia", "nvngx_dlssnr.dll", GatherFiles.Dlssnr(120)),
        (RtxFortyTag, "nvngx_dlssnr.dll", GatherFiles.Dlssnr(89, 120)),
        ("dlssnr-310.8.0", "nvngx_dlssnr.dll", GatherFiles.Dlssnr(86, 120)),
        ("dlss-310.9.16", "nvngx_dlss.dll", GatherFiles.Dll("mirror dlss")),
        ("dlssd-310.9.16", "nvngx_dlssd.dll", GatherFiles.Dll("mirror dlssd")));

    /// <summary>
    /// Native on <paramref name="facts"/> (an RTX 5070 game shipping ray reconstruction by default), the person's own add-on,
    /// ray reconstruction swapped, <paramref name="settings"/> on top.
    /// </summary>
    public Task<GatheredFiles> GatherAsync(Func<RouteSettings, RouteSettings>? settings = null, RouteId route = RouteId.Native, RouteFacts? facts = null)
    {
        RouteSettings chosen = RouteSettings.Default with { RenodxFile = OwnAddon, RayReconstructionLabel = "310.9.16" };
        if (!_nvidiaAnswered)
        {
            _ = Components.Unreachable(MirrorBed.NvidiaLatest);
            _nvidiaAnswered = true;
        }

        PlanGather gather = new(Components.Run, Components.Files, NullLogger.Instance);
        return gather.GatherAsync(new(route, facts ?? GatherFiles.Rtx50Game, (settings ?? (read => read))(chosen)), null, Components.Cancellation);
    }



    /// <summary>The download entries in the cache named <paramref name="file"/>, unpacked folders left out.</summary>
    public int Downloads(string file) => GatherFiles.Downloads(Components.Net.CacheRoot, file);

    public void Dispose()
    {
        Components.Dispose();
        _own.Dispose();
    }
}

/// <summary>What a test does with a gather bed besides a whole gather.</summary>
internal static class GatherBedRuns
{
    /// <summary>The bridge's newest release on GitHub is <c>v2.0.0</c>, its add-on served as <see cref="FetchFixtures.Addon64"/>.</summary>
    public static GatherBed Bridge(this GatherBed bed)
    {
        const string repository = "NIGos/dlss5-bridge";
        _ = bed.Components.Json(ComponentsBed.Api(repository, "/latest"), ReleaseJsonText.Release(repository, "v2.0.0", 0, "dlss5-bridge.addon64"));
        _ = bed.Components.Server.AnswerBytes(ComponentsBed.Download(repository, "v2.0.0", "dlss5-bridge.addon64"), FetchFixtures.Addon64);
        return bed;
    }

    /// <summary>Component <paramref name="id"/> gathered on its own, as its source resolves it.</summary>
    public static async Task<GatheredFiles> GatherComponentAsync(this GatherBed bed, string id)
    {
        BuildFetch fetch = new(bed.Components.Run.Mirror, bed.Components.Files, new GatheredFiles(), null, bed.Components.Cancellation);
        await new ReleaseGather(bed.Components.Run, fetch, bed.Components.Cancellation).ComponentAsync(id);
        return fetch.Gathered;
    }

    /// <summary>The requests made for the mirror's release <paramref name="tag"/>'s zip.</summary>
    public static int RequestsForMirror(this GatherBed bed, string tag) => bed.Components.Server.RequestsTo(ComponentsBed.Download(ComponentRepositories.Mirror, tag, tag + ".zip"));

    /// <summary>The file <paramref name="file"/> of <paramref name="id"/> was gathered at, full.</summary>
    public static string PathOf(GatheredFiles files, string id, string file) => ((CachedSource)files.Of(id, file).Source).Path;
}

/// <summary>The files a gather bed serves.</summary>
internal static class GatherFiles
{
    /// <summary>A 64-bit DLL large enough for NVIDIA's raw-file checks, naming <paramref name="text"/>.</summary>
    public static byte[] Dll(string text) => [.. GameFolders.Dll(text), .. new byte[NvidiaFiles.MinRawSize]];

    /// <summary>A <c>nvngx_dlssnr.dll</c> whose fatbin carries code for <paramref name="architectures"/>.</summary>
    public static byte[] Dlssnr(params int[] architectures) => [.. Dll("dlssnr"), .. FatbinTests.FatbinBytes(24, architectures)];

    public static byte[] Zip(params (string Name, byte[] Data)[] members) =>
        FetchFixtures.Archive(ArchiveFormat.Zip, [.. members.Select(member => new TestMember(member.Name, member.Data))]);

    public static string NvidiaRaw(string file) => GitHubSource.Parse(ComponentRepositories.Nvidia).Repository.RawFile("v310.9.16", NvidiaRuntimes.Folder + "/" + file).ToString();

    /// <summary>The same game on an RTX 3070.</summary>
    public static RouteFacts Rtx30Game => Rtx50Game with { Gpu = RouteFactsBeds.Card(0x10DE, "NVIDIA GeForce RTX 3070") };

    /// <summary>A 64-bit game on an RTX 5070, shipping DLSS and ray reconstruction.</summary>
    public static RouteFacts Rtx50Game =>
        RouteFactsBeds.Game(ownDlss: true, rayReconstruction: true, executable: "Game.exe") with { Gpu = RouteFactsBeds.Card(0x10DE, "NVIDIA GeForce RTX 5070") };

    /// <summary>The mirror lists <paramref name="releases"/>, newest first, each one zip asset holding the release's member.</summary>
    public static void ServeMirror(ComponentsBed components, (string Tag, string Member, byte[] Data)[] releases)
    {
        foreach (string pin in RenodxPins.Tags)
        {
            components.Server.Answer(MirrorBed.ApiTag(pin), System.Net.HttpStatusCode.NotFound, /*lang=json,strict*/ """{"message":"Not Found"}""");
        }

        _ = components.Json(MirrorBed.Api, ReleaseJsonText.List([.. releases.Select((release, age) => ReleaseJsonText.Release(ComponentRepositories.Mirror, release.Tag, age, release.Tag + ".zip"))]));
        foreach ((string tag, string member, byte[] data) in releases)
        {
            _ = components.Server.AnswerBytes(ComponentsBed.Download(ComponentRepositories.Mirror, tag, tag + ".zip"), Zip((member, data)));
        }
    }

    /// <summary>NVIDIA's latest release is <c>v310.9.16</c>, its raw runtimes served as <paramref name="dll"/>.</summary>
    public static void ServeNvidia(ComponentsBed components, byte[] dll)
    {
        components.Server.Redirect(MirrorBed.NvidiaLatest, MirrorFixtures.NvidiaLatestLocation());
        foreach (string file in new[] { "nvngx_dlss.dll", "nvngx_dlssd.dll" })
        {
            _ = components.Server.AnswerBytes(NvidiaRaw(file), dll);
        }
    }

    public static int Downloads(string cacheRoot, string file) =>
        Directory.EnumerateFiles(cacheRoot, "*", SearchOption.AllDirectories)
            .Count(path => !path.Contains("unpacked", StringComparison.Ordinal) && path.EndsWith(file, StringComparison.Ordinal));
}
