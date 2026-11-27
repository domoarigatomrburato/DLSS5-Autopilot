using Autopilot.Core.Executables;
using Autopilot.Core.Platform;
using Autopilot.Core.Resources;
using Microsoft.Extensions.Logging;

namespace Autopilot.Core.Install;

/// <summary>
/// A plan's layer registrations, decided in the order the install spec's table asks: our own layer, already registered and
/// enabled, refreshed where its files differ; else a foreign one this game's architecture can load, active and with its
/// manifest there, reused as it is; else ours, written and registered. The OpenXR layer only for a 64-bit game.
/// </summary>
/// <param name="user">The current user's registry, where our layers are registered.</param>
/// <param name="machine">The machine's registry, read for a foreign layer only.</param>
/// <param name="folder">The layer folder.</param>
/// <param name="bitness">The game's architecture.</param>
/// <param name="texts">Where the notes and warnings go.</param>
/// <param name="logger">Where the decisions are logged.</param>
internal sealed partial class LayerSteps(IUserRegistry user, IRegistry machine, string folder, Bitness bitness, SharedTexts texts, ILogger logger)
{
    /// <summary>The actions of <paramref name="step"/>.</summary>
    /// <exception cref="RegistryChangeException">The registry key cannot be read.</exception>
    /// <exception cref="InvalidDataException">The setup's manifest is not one.</exception>
    /// <exception cref="IOException">A layer file cannot be read.</exception>
    public async Task<IReadOnlyList<InstallAction>> LayerAsync(RegisterLayer step, CancellationToken cancellationToken)
    {
        var kind = LayerKind.Of(step.Layer);
        if (kind.Layer == RegisteredLayer.OpenXr && bitness != Bitness.Bits64)
        {
            LogOpenXrSkipped(logger);
            texts.Warn(Texts.Text("InstallOpenXr32Bit"));
            return [];
        }

        // The key read once: our value, and the foreign ones beside it.
        string manifest = kind.ManifestIn(folder);
        IReadOnlyList<string> names = Read(() => user.ValueNames(kind.Key), kind);
        string? ours = SharedPaths.Naming(names, manifest).FirstOrDefault();
        int? value = ours is null ? null : Value(kind, ours);
        if (value != 0 && Foreign(kind, names) is { } foreign)
        {
            return Reused(kind, foreign);
        }

        IReadOnlyList<DiskChange> made = await LayerFiles.ChangesAsync(step, kind, folder, user, value != 0 ? ours ?? manifest : null, cancellationToken);
        return Registered(kind, manifest, made);
    }

    private string? Foreign(LayerKind kind, IReadOnlyList<string> names) => ForeignLayers.Find(machine, kind, folder, names, name => Value(kind, name));

    private int? Value(LayerKind kind, string name) => Read(() => user.ReadDword(kind.Key, name), kind);

    private IReadOnlyList<InstallAction> Reused(LayerKind kind, string foreign)
    {
        LogReused(logger, kind.Shown, foreign);
        texts.Note(Texts.Format("InstallLayerReused", kind.Shown, foreign));
        return [new LayerAction(foreign, new(kind.Layer, true, foreign), [])];
    }

    private IReadOnlyList<InstallAction> Registered(LayerKind kind, string manifest, IReadOnlyList<DiskChange> made)
    {
        texts.Note(Texts.Text(kind.Layer == RegisteredLayer.OpenXr ? "InstallLayerNoteOpenXr" : "InstallLayerNoteVulkan"));
        if (kind.Layer == RegisteredLayer.OpenXr)
        {
            texts.Warn(Texts.Text("InstallLayerWarningOpenXr"));
        }

        return [new LayerAction(manifest, new(kind.Layer, false, manifest), made)];
    }

    // A key Windows will not let us read is a layer that cannot be registered.
    private static T Read<T>(Func<T> read, LayerKind kind)
    {
        try
        {
            return read();
        }
        catch (Exception refused) when (RegistryChange.IsRefusal(refused))
        {
            throw RegistryChangeException.Read($@"HKCU\{kind.Key}", refused);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "VR: the OpenXR layer is 64-bit only - skipped")]
    private static partial void LogOpenXrSkipped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "ReShade's {Layer} layer is already registered ({Manifest}); reuse it")]
    private static partial void LogReused(ILogger logger, string layer, string manifest);
}
