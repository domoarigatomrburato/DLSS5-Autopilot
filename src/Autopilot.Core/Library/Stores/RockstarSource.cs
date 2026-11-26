using System.Text.RegularExpressions;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Library.Stores;

/// <summary>
/// The Rockstar Games Launcher's installed games, from the uninstall keys whose uninstall command is
/// the launcher's with an <c>uninstall=&lt;title&gt;</c>. The launcher and Social Club register an
/// install folder the same way and are left out.
/// </summary>
public sealed partial class RockstarSource(IRegistry registry) : IGameSource
{
    private static readonly string[] _notGames = ["launcher", "rockstar games launcher", "rockstar games social club", "social club", "socialclub"];

    public GameSource Source => GameSource.Rockstar;

    public Task<IReadOnlyList<StoreGame>> FindAsync(CancellationToken cancellationToken)
    {
        FoundGames found = new();
        foreach (UninstallEntry entry in UninstallEntry.ReadAll(registry))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsGame(entry) && DrivePaths.IsDirectory(entry.InstallLocation))
            {
                string folder = entry.InstallLocation!;
                string name = string.IsNullOrWhiteSpace(entry.DisplayName) ? StoreFiles.FolderName(folder) : entry.DisplayName.Trim();
                found.Add(new(name, folder, GameSource.Rockstar, null));
            }
        }

        return Task.FromResult(found.Games);
    }

    private static bool IsGame(UninstallEntry entry) =>
        entry.UninstallString is not null
        && RockstarTitle().Match(entry.UninstallString) is { Success: true } match
        && !IsNotGame(match.Groups["title"].Value)
        && !IsNotGame(entry.DisplayName)
        && !IsNotGame(entry.KeyName);

    private static bool IsNotGame(string? name) => name is not null && _notGames.Contains(name.Trim(), StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex("""(?:Launcher|uninstall)\.exe.+uninstall=(?<title>[^\s"]+)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RockstarTitle();
}
