using Autopilot.Core.Platform;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Library.Emulators;

/// <summary>Reads which render backend an emulator is set to, from its own config file. Nothing is written.</summary>
public static class EmulatorConfig
{
    /// <summary>
    /// The backend the config of the emulator at <paramref name="executable"/> names: the first config
    /// file that exists, in the emulator's own search order (beside the executable when it runs
    /// portable, else the person's folders), then the one setting. "unknown" when there is no file,
    /// no value, or the emulator has no config this tool reads.
    /// </summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task<EmulatorBackend> ReadBackendAsync(EmulatorProfile profile, string executable, HostFolders folders, CancellationToken cancellationToken)
    {
        if (BackendSettings.For(profile) is not { } setting)
        {
            return Unknown(null);
        }

        ConfigPlace place = new(Path.GetDirectoryName(executable) ?? "", Path.GetFileName(executable), folders);
        IEnumerable<string?> candidates = await setting.Locate(place, cancellationToken);
        if (candidates.OfType<string>().FirstOrDefault(DrivePaths.IsFile) is not { } file)
        {
            return Unknown(null);
        }

        return await ConfigText.ReadAsync(file, cancellationToken) is { } bytes && setting.Value(bytes) is { Length: > 0 } raw
            ? new EmulatorBackend(setting.NameOf(raw), setting.IsDxgi(raw), file)
            : Unknown(file);
    }

    private static EmulatorBackend Unknown(string? file) => new(Texts.Text("EmulatorBackendUnknown"), false, file);
}
