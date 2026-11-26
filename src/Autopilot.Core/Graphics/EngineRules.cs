using Autopilot.Core.Resources;

namespace Autopilot.Core.Graphics;

/// <summary>Engines whose layout settles the renderer by itself, in the order they are asked.</summary>
internal static class EngineRules
{
    public static async Task<ApiDecision?> DecideAsync(GameFolder game, CancellationToken cancellationToken)
    {
        // Unity names every backend it can drive, and picks Direct3D 11 on Windows unless told otherwise.
        if (game.Files.ContainsKey(KnownFiles.UnityPlayer))
        {
            return new ApiDecision(GraphicsApi.DX11, ApiReason.Unity, Texts.Text("ApiUnity"));
        }

        return await UnrealClassic.DecideAsync(game, cancellationToken)
            ?? await Unreal3.DecideAsync(game, cancellationToken)
            ?? Unreal(game);
    }

    // Unreal 4 and 5 draw with Direct3D 11 or 12; the opengl32.dll they name is an RHI their Windows builds do not use.
    private static ApiDecision? Unreal(GameFolder game) =>
        UnrealLayout.BinariesFolder(game) is { } binaries
            ? new ApiDecision(GraphicsApi.Dxgi, ApiReason.Unreal, Texts.Format("ApiUnreal", binaries))
            : null;
}
