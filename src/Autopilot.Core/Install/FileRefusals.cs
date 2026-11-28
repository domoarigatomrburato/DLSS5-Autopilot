using Autopilot.Core.Executables;
using Autopilot.Core.Install.Plans;
using Autopilot.Core.Resources;
using Autopilot.Core.Routes;

namespace Autopilot.Core.Install;

/// <summary>The refusals the files in the way and the person's own files decide: the proxy name, their frame generation files and their own add-on. Read-only.</summary>
internal static class FileRefusals
{
    /// <summary>
    /// Why <paramref name="files"/> cannot be placed: OptiScaler loading under a name they need, the tool's copy
    /// gone, or a file under one of their names that is neither on <paramref name="prior"/>'s record nor the same
    /// bytes as the copy; null when they can.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task<string?> OwnFilesAsync(RefusalRequest request, RouteSettings read, OwnFrameGeneration files, PriorInstall prior, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> names = files.FileNames();
        if (read.OptiScalerLoadsAs is { } proxy && names.Contains(proxy.FileName(), StringComparer.OrdinalIgnoreCase))
        {
            return Texts.Format("RefusalOwnFilesName", proxy.FileName(), files.Label());
        }

        string? store = request.OwnFrameGenerationStore is { } root ? Path.Join(root, files.StoreFolder()) : null;
        if (store is null || !names.All(name => File.Exists(Path.Join(store, name))))
        {
            return Texts.Format("RefusalOwnFilesGone", files.Label());
        }

        foreach (string name in names)
        {
            string there = Path.Join(request.InstallFolder, name);
            if (File.Exists(there) && prior.At(there) is null && !await HoldsCopyAsync(there, Path.Join(store, name), cancellationToken))
            {
                return Texts.Format("RefusalOwnFilesTaken", name, files.Label(), request.InstallFolder);
            }
        }

        return null;
    }

    // Whether the file at there is the tool's copy, byte for byte: its size first, then its hash.
    private static async Task<bool> HoldsCopyAsync(string there, string copy, CancellationToken cancellationToken)
    {
        try
        {
            return await FileFacts.OfAsync(copy, cancellationToken) is { } facts && await facts.IsAtAsync(there, cancellationToken);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Why the person's own add-on at <paramref name="addOn"/> cannot be used: it must be there and a 64-bit DLL, as ReShade's 64-bit add-ons are.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task<string?> LocalAddOnAsync(string addOn, CancellationToken cancellationToken)
    {
        if (!File.Exists(addOn))
        {
            return Texts.Format("RefusalLocalAddOnMissing", addOn);
        }

        return await PeImage.BitnessOfAsync(addOn, cancellationToken) == Bitness.Bits64 ? null : Texts.Format("RefusalLocalAddOnNot64", addOn);
    }

    /// <summary>
    /// Why ReShade cannot take its name on <paramref name="route"/>: a file under it that is neither ReShade, nor ours from the
    /// previous install, nor a hand-installed OptiScaler (which is moved aside), named for what it was recognised as; null
    /// when the name is free, or the route places no proxy.
    /// </summary>
    public static string? ProxyTaken(RouteId route, RouteFacts facts, RouteSettings read, FolderSurvey survey)
    {
        ArgumentNullException.ThrowIfNull(survey);
        if (!RouteComponents.IsReShadeRoute(route) || ReShadeBase.ProxyName(route, facts, read) is not { } proxy)
        {
            return null;
        }

        return survey.At(survey.Beside(proxy))?.Content switch
        {
            ProxyContent.Dxvk => Texts.Format("RefusalProxyTakenBy", proxy, "DXVK"),
            ProxyContent.Reframework => Texts.Format("RefusalProxyTakenBy", proxy, "REFramework"),
            ProxyContent.SpecialK => Texts.Format("RefusalProxyTakenBy", proxy, "Special K"),
            ProxyContent.Other => Texts.Format("RefusalProxyTaken", proxy),
            _ => null,
        };
    }
}
