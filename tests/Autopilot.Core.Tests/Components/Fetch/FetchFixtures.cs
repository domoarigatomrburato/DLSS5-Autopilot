using System.Text;
using Autopilot.Core.Components;
using Autopilot.Core.Components.Fetch;
using Autopilot.Core.Net;
using Autopilot.Core.Tests.Archives;
using Autopilot.Core.Tests.Executables;
using Autopilot.Core.Tests.Net;

namespace Autopilot.Core.Tests.Components.Fetch;

/// <summary>Resolved components served from the fake server, and the archives and add-ons they hold.</summary>
internal static class FetchFixtures
{
    public static readonly byte[] Addon64 = new TestPe().ToBytes();

    public static readonly byte[] Addon32 = new TestPe(TestPe.I386).ToBytes();

    public static readonly byte[] NotAnAddon = Encoding.UTF8.GetBytes("MZ but nothing else");

    /// <summary>A file of an archive, holding its own name as text.</summary>
    public static TestMember Text(string name) => TestMember.Text(name, Path.GetFileName(name));

    /// <summary>A component of one download, served from the fake server and checked by its SHA-256.</summary>
    public static ComponentRelease Served(ComponentsBed bed, string id, string fileName, byte[] content, params string[] files)
    {
        string url = ComponentsBed.Download("owner/repo", "v1.0.0", fileName);
        _ = bed.Server.AnswerBytes(url, content);
        return Release(id, [Download(fileName, url, content)], [.. files.Select(file => new ExpectedFile(file))]);
    }

    public static ComponentRelease Release(string id, ComponentDownload[] downloads, ExpectedFile[] files) =>
        new(id, new("v1.0.0"), downloads, files, new(ReleaseOrigin.Api, TimeSpan.Zero));

    public static ComponentDownload Download(string fileName, string url, byte[] content) =>
        new(fileName, new(url), new Sha256Verification(CacheFixtures.DigestOf(content)));

    /// <summary>An archive of <paramref name="members"/>, as bytes.</summary>
    public static byte[] Archive(ArchiveFormat format, params TestMember[] members)
    {
        string path = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N") + (format == ArchiveFormat.Zip ? ".zip" : ".tar.gz"));
        try
        {
            TestArchives.Write(format, path, members);
            return File.ReadAllBytes(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>The path of a fetched file, with '/' between folders.</summary>
    public static string PathOf(FetchedComponent fetched, string file) => fetched.Files[file].Replace('\\', '/');
}
