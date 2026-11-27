using Autopilot.Core.Executables;
using Autopilot.Core.Install;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Graphics;

/// <summary>
/// Decides the graphics API a game draws with from its executable's tables and the files beside
/// it, and says why in words that name the evidence. Nothing is executed or loaded; files are read
/// as bytes, and a bad file beside the executable is no evidence rather than an error.
/// </summary>
internal static class GraphicsApiDecision
{
    // The static table's ranking, the d3d10 family in d3d10's place.
    private static readonly (string Name, GraphicsApi Api)[] _delayRanking =
    [
        .. KnownFiles.Renderers.SelectMany(renderer => renderer.Api == GraphicsApi.DX10 ? KnownFiles.D3d10Family.Select(name => (name, renderer.Api)) : [renderer]),
    ];

    /// <summary>
    /// The graphics API of the game whose executable is at <paramref name="executablePath"/>, as
    /// read into <paramref name="reading"/>. A protected executable has no imports and an unknown
    /// bitness: only the files beside it decide.
    /// </summary>
    /// <param name="executablePath">The game's executable.</param>
    /// <param name="reading">What <see cref="Executable.ReadAsync"/> read from it.</param>
    /// <param name="search">Where else the decision may look.</param>
    /// <param name="record">The install record found for the game, or null: a file of ours beside the executable is not the game's evidence.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    public static async Task<ApiDecision> DecideAsync(string executablePath, ExecutableReading reading, ApiSearch search, RecordedInstall? record, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var game = GameFolder.Open(executablePath, reading, search, record, cancellationToken);
        ApiDecision decision = await DecideAsync(game, cancellationToken);
        // Windows ships no 64-bit d3d8.dll, so a 64-bit executable that names it does not draw with it.
        return decision.Api == GraphicsApi.DX8 && game.Bitness == Bitness.Bits64
            ? new ApiDecision(GraphicsApi.Unknown, ApiReason.None, Texts.Format("ApiDx8On64", decision.Explanation))
            : decision;
    }

    // The first rule that applies decides; a DXGI-family import decides before OpenGL is looked at.
    private static async Task<ApiDecision> DecideAsync(GameFolder game, CancellationToken cancellationToken)
    {
        IReadOnlySet<string> imports = game.Imports;
        if (imports.Contains(KnownFiles.D3d12))
        {
            return Static(GraphicsApi.DX12, "ApiStaticImport", KnownFiles.D3d12);
        }

        if (imports.Contains(KnownFiles.D3d11))
        {
            return await D3d11Async(game, cancellationToken);
        }

        if (KnownFiles.D3d10Family.FirstOrDefault(imports.Contains) is { } d3d10)
        {
            return Static(GraphicsApi.DX10, "ApiStaticImport", d3d10);
        }

        if (imports.Contains(KnownFiles.Dxgi))
        {
            return new ApiDecision(GraphicsApi.Dxgi, ApiReason.StaticImport, Texts.Text("ApiStaticDxgi"));
        }

        return await WithoutDxgiAsync(game, cancellationToken);
    }

    // A D3D12 Agility SDK, or Frame Generation in its Unreal plugins, makes a d3d11.dll game a D3D12 one.
    private static async Task<ApiDecision> D3d11Async(GameFolder game, CancellationToken cancellationToken)
    {
        if (await LocalEvidence.AgilitySdkAsync(game, cancellationToken) is { } agility)
        {
            return new ApiDecision(GraphicsApi.DX12, ApiReason.AgilitySdk, Texts.Format("ApiD3d11Promoted", agility.Words));
        }

        if (await UnrealLayout.D3d12PluginAsync(game, cancellationToken) is var (plugin, runtime))
        {
            return new ApiDecision(GraphicsApi.DX12, ApiReason.UnrealPlugin, Texts.Format("ApiUnrealPlugin", plugin, runtime));
        }

        return Static(GraphicsApi.DX11, "ApiStaticImport", KnownFiles.D3d11);
    }

    private static async Task<ApiDecision> WithoutDxgiAsync(GameFolder game, CancellationToken cancellationToken)
    {
        IReadOnlySet<string> imports = game.Imports;
        if (imports.Contains(KnownFiles.Vulkan))
        {
            return Static(GraphicsApi.Vulkan, "ApiStaticNoDxgi", KnownFiles.Vulkan);
        }

        if (imports.Contains(KnownFiles.OpenGL))
        {
            return await EngineRules.DecideAsync(game, cancellationToken) ?? Static(GraphicsApi.OpenGL, "ApiStaticNoDxgi", KnownFiles.OpenGL);
        }

        if (imports.Contains(KnownFiles.D3d9))
        {
            return await D3d9Sequence.DecideAsync(game, cancellationToken);
        }

        if (imports.Contains(KnownFiles.D3d8))
        {
            return Static(GraphicsApi.DX8, "ApiStaticNoDxgi", KnownFiles.D3d8);
        }

        return await NoImportAsync(game, cancellationToken);
    }

    private static async Task<ApiDecision> NoImportAsync(GameFolder game, CancellationToken cancellationToken)
    {
        if (await LocalEvidence.AgilitySdkAsync(game, cancellationToken) is { } agility)
        {
            return new ApiDecision(GraphicsApi.DX12, ApiReason.AgilitySdk, Texts.Format("ApiNoImportPromoted", agility.Words));
        }

        // A delay-load is a dependency the linker recorded; the loader resolves it on first use.
        if (_delayRanking.FirstOrDefault(renderer => game.DelayImports.Contains(renderer.Name)) is ({ } delayed, var api))
        {
            return new ApiDecision(api, ApiReason.DelayLoad, Texts.Format("ApiDelayLoad", delayed));
        }

        return await RuntimeSearch.FindAsync(game, cancellationToken) switch
        {
            EngineFinding engine => engine.Decision,
            NameFinding name => await RunTimeAsync(game, name, cancellationToken),
            _ => new ApiDecision(GraphicsApi.Unknown, ApiReason.None, Texts.Text("ApiUnknown")),
        };
    }

    // DLSS has no 32-bit build, so the game's own DLSS promotes a run-time d3d9.dll only beside a 64-bit executable.
    private static async Task<ApiDecision> RunTimeAsync(GameFolder game, NameFinding name, CancellationToken cancellationToken)
    {
        if (name.Api == GraphicsApi.DX9 && game.Bitness != Bitness.Bits32 && await LocalEvidence.ShipsDlssAsync(game, cancellationToken) is { } dlss)
        {
            return new ApiDecision(GraphicsApi.DX12, dlss.Reason, Texts.Format("ApiRunTimeDlss", name.Where, dlss.Words));
        }

        return new ApiDecision(name.Api, name.Source, Texts.Format("ApiRunTime", name.Name, name.Where));
    }

    private static ApiDecision Static(GraphicsApi api, string text, string dll) =>
        new(api, ApiReason.StaticImport, Texts.Format(text, dll));
}
