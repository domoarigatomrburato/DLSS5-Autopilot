using Autopilot.Core.Resources;

namespace Autopilot.Core.Graphics;

/// <summary>What the files beside the executable say about a modern renderer, each limited by bitness.</summary>
internal static class LocalEvidence
{
    /// <summary>
    /// A D3D12 Agility SDK beside the executable: <c>D3D12\D3D12Core.dll</c>, or the game's own DLSS
    /// Frame Generation or Ray Reconstruction (D3D12-only), unless another DLSS tool brought them.
    /// </summary>
    public static async Task<Evidence?> AgilitySdkAsync(GameFolder game, CancellationToken cancellationToken)
    {
        if (await AgilityCoreAsync(game, cancellationToken) is { } core)
        {
            return core;
        }

        string? runtime = await GameNgxAsync(game, KnownFiles.D3d12OnlyRuntimes, cancellationToken);
        return runtime is null ? null : new Evidence(ApiReason.AgilitySdk, Texts.Format("ApiEvidenceFrameGeneration", runtime));
    }

    /// <summary>
    /// The game's own DLSS runtime (an NGX file it shipped, unless another DLSS tool brought them),
    /// else a D3D12 Agility SDK.
    /// </summary>
    public static async Task<Evidence?> ShipsDlssAsync(GameFolder game, CancellationToken cancellationToken) =>
        await GameNgxAsync(game, KnownFiles.NgxRuntimes, cancellationToken) is { } runtime
            ? new Evidence(ApiReason.DlssFiles, runtime)
            : await AgilityCoreAsync(game, cancellationToken);

    private static async Task<Evidence?> AgilityCoreAsync(GameFolder game, CancellationToken cancellationToken)
    {
        string core = Path.Join(game.Folder, KnownFiles.AgilityFolder, KnownFiles.AgilityCore);
        return File.Exists(core) && await game.FitsAsync(core, cancellationToken)
            ? new Evidence(ApiReason.AgilitySdk, Texts.Text("ApiEvidenceAgilityCore"))
            : null;
    }

    // With another DLSS tool in the folder (not counting ours), no NGX file there is the game's.
    private static async Task<string?> GameNgxAsync(GameFolder game, IReadOnlyList<string> runtimes, CancellationToken cancellationToken)
    {
        if (game.Files.Keys.Any(name => !game.Ours.Contains(name) && KnownFiles.IsForeignNgx(name)))
        {
            return null;
        }

        foreach (string runtime in runtimes)
        {
            if (game.GameFile(runtime) is { } path && await game.FitsAsync(path, cancellationToken))
            {
                return Path.GetFileName(path);
            }
        }

        return null;
    }
}
