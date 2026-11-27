using System.Collections.Frozen;

namespace Autopilot.Core.Graphics;

/// <summary>
/// The Unreal Engine 4/5 layout, by shape: <c>&lt;root&gt;\&lt;Project&gt;\Binaries\Win64\&lt;game&gt;.exe</c>
/// with the engine's own <c>&lt;root&gt;\Engine</c> folder, and NVIDIA's plugins under it.
/// </summary>
internal static class UnrealLayout
{
    private static readonly FrozenSet<string> _binaryFolders = new[] { "Win64", "WinGDK", "WinARM64" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] _plugins = ["Streamline", "DLSS", "StreamlineCore", "StreamlineDLSSG"];

    /// <summary>The executable's binaries folder name (such as "Win64") in an Unreal layout, or null.</summary>
    public static string? BinariesFolder(GameFolder game)
    {
        DirectoryInfo? binaries = Directory.GetParent(game.Folder);
        DirectoryInfo? root = binaries?.Parent?.Parent;
        string platform = Path.GetFileName(game.Folder);
        bool shaped = _binaryFolders.Contains(platform)
            && string.Equals(binaries?.Name, "Binaries", StringComparison.OrdinalIgnoreCase)
            && root is not null
            && Directory.Exists(Path.Join(root.FullName, "Engine"));
        return shaped ? platform : null;
    }

    /// <summary>
    /// DLSS Frame Generation or Ray Reconstruction in NVIDIA's Unreal plugins, at fixed paths
    /// (no walk): the plugin's name and the file's, or null.
    /// </summary>
    public static async Task<(string Plugin, string File)?> D3d12PluginAsync(GameFolder game, CancellationToken cancellationToken)
    {
        if (BinariesFolder(game) is null)
        {
            return null;
        }

        string nvidia = Path.Join(game.Folder, "..", "..", "..", "Engine", "Plugins", "Runtime", "Nvidia");
        if (!Directory.Exists(nvidia))
        {
            return null;
        }

        foreach (string plugin in _plugins)
        {
            foreach (string runtime in KnownFiles.D3d12OnlyRuntimes)
            {
                string path = Path.GetFullPath(Path.Join(nvidia, plugin, "Binaries", "ThirdParty", "Win64", runtime));
                if (File.Exists(path) && await game.FitsAsync(path, cancellationToken))
                {
                    return (plugin, runtime);
                }
            }
        }

        return null;
    }
}
