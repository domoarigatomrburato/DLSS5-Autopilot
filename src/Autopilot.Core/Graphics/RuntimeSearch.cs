using Autopilot.Core.Resources;

namespace Autopilot.Core.Graphics;

/// <summary>What the run-time renderer search found: an engine's verdict, or a renderer name and where it was seen.</summary>
internal abstract record RuntimeFinding;

/// <summary>An engine rule settled the renderer.</summary>
internal sealed record EngineFinding(ApiDecision Decision) : RuntimeFinding;

/// <summary>A renderer DLL name, the kind of place it was seen in, and that place in words.</summary>
internal sealed record NameFinding(string Name, GraphicsApi Api, ApiReason Source, string Where) : RuntimeFinding
{
    /// <summary>A record (another DLL's import table), not a mere string in some bytes.</summary>
    public bool IsRecord => Source == ApiReason.SiblingImport;

    /// <summary>The best-ranked of <paramref name="names"/>, seen at <paramref name="where"/>, or null.</summary>
    public static NameFinding? Best(IReadOnlySet<string> names, ApiReason source, string where) =>
        KnownFiles.Renderers.Where(renderer => names.Contains(renderer.Name))
            .Select(renderer => new NameFinding(renderer.Name, renderer.Api, source, where))
            .FirstOrDefault();
}

/// <summary>
/// The renderer a game with no static graphics import loads by name: an engine rule, then the
/// executable's own strings, then the DLLs beside it (import tables, then the strings of the largest).
/// </summary>
internal static class RuntimeSearch
{
    public static async Task<RuntimeFinding?> FindAsync(GameFolder game, CancellationToken cancellationToken)
    {
        if (await EngineRules.DecideAsync(game, cancellationToken) is { } engine)
        {
            return new EngineFinding(engine);
        }

        NameFinding? own = await InExecutableAsync(game, cancellationToken);
        // A Direct3D name in the exe stands: the DLLs beside a game (Bink, CEF, SDL) name renderers of their own.
        if (own is not null && !KnownFiles.Ambiguous.Contains(own.Name))
        {
            return own;
        }

        // Engines that can drive several backends name opengl32.dll or vulkan-1.dll without using them.
        List<NameFinding> seen = own is null ? [] : [own];
        seen.AddRange(await SiblingDlls.FindAsync(game, cancellationToken));
        return Best(seen);
    }

    private static async Task<NameFinding?> InExecutableAsync(GameFolder game, CancellationToken cancellationToken) =>
        NameFinding.Best(await NameSearch.NamesInAsync(game.Executable, cancellationToken), ApiReason.RunTimeName, Texts.Text("ApiWhereExe"));

    // Ranked as the static table; for one name, the first place it was seen.
    private static NameFinding? Best(List<NameFinding> seen) =>
        KnownFiles.Renderers.Select(renderer => seen.Find(found => found.Name == renderer.Name)).FirstOrDefault(found => found is not null);
}
