using Autopilot.Core.Components;
using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Components.Fetch;
using Autopilot.Core.Components.Sources;

namespace Autopilot.Core.Install.Plans;

/// <summary>The components a gather takes as their sources resolve them: ReShade's setup, and any id <see cref="ComponentSources.ResolveAsync"/> knows.</summary>
/// <param name="run">The job's view of the sources.</param>
/// <param name="fetch">Fetches and records each file.</param>
/// <param name="cancellation">The gather's token.</param>
internal sealed class ReleaseGather(ComponentRun run, BuildFetch fetch, CancellationToken cancellation)
{
    /// <summary>Component <paramref name="id"/> as its source resolves it: every file it holds, its version and where it came from.</summary>
    public async Task ComponentAsync(string id)
    {
        ComponentRelease release = await run.Sources.ResolveAsync(id, cancellation);
        FetchedComponent fetched = await fetch.FetchAsync(release);
        foreach (ExpectedFile file in release.Files)
        {
            // An optional file the release lacks is not there.
            if (fetched.Files.TryGetValue(file.Path, out string? path))
            {
                await fetch.AddAsync(id, file.Path, path);
            }
        }

        fetch.Gathered.Version(id, release.Version.Label);
        fetch.Gathered.Note(release.Notice);
    }

    /// <summary>The setup's runtimes and layer manifests, the manifests' bytes kept for the layer step.</summary>
    public async Task ReShadeAsync()
    {
        ReShadeInstaller installer = await run.ReShade.ResolveAsync(cancellation);
        FetchedComponent fetched = await fetch.FetchAsync(installer.Release);
        foreach (string file in ReShadeInstaller.Files)
        {
            await (file is ReShadeInstaller.Manifest64 or ReShadeInstaller.Manifest32
                ? fetch.AddContentAsync(ComponentIds.ReShade, file, fetched.Files[file])
                : fetch.AddAsync(ComponentIds.ReShade, file, fetched.Files[file]));
        }

        fetch.Gathered.Version(ComponentIds.ReShade, installer.Version.Label);
    }
}
