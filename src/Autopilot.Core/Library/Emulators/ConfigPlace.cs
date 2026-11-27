using Autopilot.Core.Platform;

namespace Autopilot.Core.Library.Emulators;

/// <summary>Where an emulator looks for its config: beside its executable, or in the person's folders.</summary>
/// <param name="Folder">The executable's folder.</param>
/// <param name="ExecutableName">The executable's file name.</param>
/// <param name="Folders">The machine's well-known folders.</param>
internal sealed record ConfigPlace(string Folder, string ExecutableName, HostFolders Folders)
{
    /// <summary>A file beside the executable.</summary>
    public string Beside(params string[] parts) => Path.Join([Folder, .. parts]);

    /// <summary>Whether a marker file (<c>portable.txt</c>) sits beside the executable.</summary>
    public bool Marked(params string[] markers) => markers.Any(marker => DrivePaths.IsFile(Beside(marker)));

    /// <summary>A path under the person's Documents folder; null when it is not known.</summary>
    public string? InDocuments(params string[] parts) => Under(Folders.Documents, parts);

    /// <summary>A path under <c>%LOCALAPPDATA%</c>; null when it is not set.</summary>
    public string? InLocalAppData(params string[] parts) => Under(Folders.LocalAppData, parts);

    /// <summary>A path under <c>%APPDATA%</c>; null when it is not set.</summary>
    public string? InAppData(params string[] parts) => Under(Folders.AppData, parts);

    private static string? Under(string? folder, string[] parts) => folder is null ? null : Path.Join([folder, .. parts]);
}
