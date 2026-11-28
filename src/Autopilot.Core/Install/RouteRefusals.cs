using Autopilot.Core.Components.Catalog;
using Autopilot.Core.Graphics;
using Autopilot.Core.Resources;
using Autopilot.Core.Routes;

namespace Autopilot.Core.Install;

/// <summary>The refusals the game, the route, the settings and the card decide before anything is gathered. Read-only.</summary>
internal static class RouteRefusals
{
    /// <summary>What the game, the route, the settings and the card refuse, in the order checked.</summary>
    /// <param name="request">The install asked for.</param>
    /// <param name="prior">The folder's previous install, which vouches for the files it placed.</param>
    /// <param name="cancellationToken">Stops the reads.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task<IReadOnlyList<InstallBlocker>> CheckAsync(RefusalRequest request, PriorInstall prior, CancellationToken cancellationToken)
    {
        RouteSettings read = ShownSettings.Reset(request.Route.Route, request.Facts, request.Settings);
        List<InstallBlocker> found = [.. Game(request), .. await RemixAsync(request, read, cancellationToken), .. Card(request, read)];
        if (read.MotionProvider == MotionProvider.QuantMotion)
        {
            found.Add(new(BlockerKind.MotionProvider, Texts.Text("RouteProviderQuantMotion")));
        }

        if (read.OwnFrameGeneration is { } files && await FileRefusals.OwnFilesAsync(request, read, files, prior, cancellationToken) is { } own)
        {
            found.Add(new(BlockerKind.OwnFrameGeneration, own));
        }

        if (read.RenodxFile is { } addOn && await FileRefusals.LocalAddOnAsync(addOn, cancellationToken) is { } local)
        {
            found.Add(new(BlockerKind.LocalAddOn, local));
        }

        if (request.Survey is { } survey && FileRefusals.ProxyTaken(request.Route.Route, request.Facts, read, survey) is { } taken)
        {
            found.Add(new(BlockerKind.ProxyTaken, taken));
        }

        return found;
    }

    // The game as read: the library's own verdict first, then an API no route can be installed for.
    private static IEnumerable<InstallBlocker> Game(RefusalRequest request)
    {
        if (request.Game.Unsupported is { } unsupported)
        {
            yield return new(BlockerKind.Game, Texts.Format("RefusalGame", unsupported));
        }
        else if (request.Facts.Api is GraphicsApi.Unknown or GraphicsApi.Dxgi)
        {
            yield return new(BlockerKind.Api, Texts.Text("RefusalApi"));
        }
    }

    // A Remix game takes the Remix route only, which needs a runtime, and one with the neural pass unless it is swapped.
    private static async Task<IEnumerable<InstallBlocker>> RemixAsync(RefusalRequest request, RouteSettings read, CancellationToken cancellationToken)
    {
        string? trex = request.Facts.Runtimes.RemixRuntime;
        bool remix = request.Route.Route == RouteId.Remix;
        if (trex is not null && !remix)
        {
            return [new(BlockerKind.RemixOnly, Texts.Text("RefusalRemixOnly"))];
        }

        if (remix && trex is null)
        {
            return [new(BlockerKind.NoRemixRuntime, Texts.Text("RefusalNoRemixRuntime"))];
        }

        return remix && !read.SwapRemixRuntime ? await NeuralPassAsync(trex!, cancellationToken) : [];
    }

    // Nvidia's own runtime has no neural pass; one that is missing or cannot be read is named, never taken for it.
    private static async Task<IEnumerable<InstallBlocker>> NeuralPassAsync(string trex, CancellationToken cancellationToken)
    {
        string runtime = RemixNeuralPass.RuntimeOf(trex);
        if (!File.Exists(runtime))
        {
            return [new(BlockerKind.RemixRuntimeUnreadable, Texts.Format("RefusalRemixRuntimeMissing", runtime))];
        }

        return await RemixNeuralPass.HasAsync(trex, cancellationToken) switch
        {
            null => [new(BlockerKind.RemixRuntimeUnreadable, Texts.Format("RefusalRemixRuntimeUnreadable", runtime))],
            false => [new(BlockerKind.RemixWithoutNeuralPass, Texts.Text("RefusalRemixNoNeuralPass"))],
            true => [],
        };
    }

    // The card no route can run on, unless "install anyway"; the RTX 40 MFG package on another card. Both read the
    // card in the facts, as the multi-frame generation row does.
    private static IEnumerable<InstallBlocker> Card(RefusalRequest request, RouteSettings read)
    {
        if (request.Facts.Gpu?.Refusal is { } refusal && !request.IgnoreGpuMismatch)
        {
            yield return new(BlockerKind.Card, Texts.Format("RefusalCard", refusal.Explain()));
        }

        if (request.Route.Route == RouteId.OptiScaler && read.OptiScaler.CardRefusal(request.Facts.Gpu?.Card) is { } card)
        {
            yield return new(BlockerKind.OptiScalerBuild, card);
        }
    }
}
