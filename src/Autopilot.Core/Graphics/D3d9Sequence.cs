using Autopilot.Core.Executables;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Graphics;

/// <summary>
/// An executable that imports d3d9.dll and nothing newer. That import is the weakest evidence in
/// the file (engines keep it for a launcher, a video player or an old dialog), so every other kind
/// of evidence is asked first, and the first hit decides.
/// </summary>
internal static class D3d9Sequence
{
    private const string Direct3D12 = "Direct3D 12";
    private const string Direct3D11 = "Direct3D 11";

    public static async Task<ApiDecision> DecideAsync(GameFolder game, CancellationToken cancellationToken)
    {
        // 1, 2. The game's own DLSS runtime, or a D3D12 Agility SDK: NGX never runs on DirectX 9. The
        // Agility SDK's other evidence, the game's own Frame Generation or Ray Reconstruction, is NGX too.
        if (await LocalEvidence.ShipsDlssAsync(game, cancellationToken) is { } dlss)
        {
            return new ApiDecision(GraphicsApi.DX12, dlss.Reason, Texts.Format("ApiD3d9Dlss", dlss.Words));
        }

        // 3, 4. A delay-loaded renderer; a bare dxgi.dll does not count, DirectX 9 games enumerate displays with it.
        if (DelayLoaded(game) is { } delayed)
        {
            return delayed;
        }

        // 5. Only for a 64-bit executable: a record of a modern renderer, never a string in some bytes.
        if (game.Bitness == Bitness.Bits64 && Modern(await RuntimeSearch.FindAsync(game, cancellationToken)) is { } modern)
        {
            return modern;
        }

        return new ApiDecision(GraphicsApi.DX9, ApiReason.StaticImport, Texts.Format("ApiStaticNoDxgi", KnownFiles.D3d9));
    }

    private static ApiDecision? DelayLoaded(GameFolder game)
    {
        foreach ((string name, GraphicsApi api, string words) in new[] { (KnownFiles.D3d12, GraphicsApi.DX12, Direct3D12), (KnownFiles.D3d11, GraphicsApi.DX11, Direct3D11) })
        {
            if (game.DelayImports.Contains(name))
            {
                return new ApiDecision(api, ApiReason.DelayLoad, Texts.Format("ApiD3d9DelayLoad", name, words));
            }
        }

        return null;
    }

    private static ApiDecision? Modern(RuntimeFinding? finding) => finding switch
    {
        EngineFinding { Decision.Api: GraphicsApi.DX11 or GraphicsApi.DX12 or GraphicsApi.Dxgi } engine =>
            engine.Decision with { Explanation = Texts.Format("ApiD3d9Engine", engine.Decision.Explanation) },
        NameFinding { IsRecord: true, Api: GraphicsApi.DX11 or GraphicsApi.DX12 } name =>
            new ApiDecision(name.Api, name.Source, Texts.Format("ApiD3d9RunTime", name.Name, name.Where, name.Api == GraphicsApi.DX12 ? Direct3D12 : Direct3D11)),
        _ => null,
    };
}
