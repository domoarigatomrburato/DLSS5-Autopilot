using System.Text.RegularExpressions;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Library.Stores;

/// <summary>
/// GOG's installed games, from their uninstall keys (<c>&lt;id&gt;_is1</c>, published by GOG.com).
/// An entry whose <c>goggame-&lt;id&gt;.info</c> says it is DLC is left out; the name is GOG's own.
/// </summary>
public sealed partial class GogSource(IRegistry registry) : IGameSource
{
    private const string GamesKey = @"SOFTWARE\GOG.com\Games";

    public GameSource Source => GameSource.Gog;

    public async Task<IReadOnlyList<StoreGame>> FindAsync(CancellationToken cancellationToken)
    {
        FoundGames found = new(GameSource.Gog);
        foreach (UninstallEntry entry in UninstallEntry.ReadAll(registry))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (GameId(entry) is { } id && !found.Has(entry.InstallLocation!) && await GameAsync(id, entry, cancellationToken) is { } game)
            {
                found.Add(game);
            }
        }

        return found.Games;
    }

    // An installer key of GOG's whose folder is there.
    private static string? GameId(UninstallEntry entry) =>
        GogKey().Match(entry.KeyName) is { Success: true } match
        && string.Equals(entry.Publisher?.Trim(), "GOG.com", StringComparison.OrdinalIgnoreCase)
        && DrivePaths.IsDirectory(entry.InstallLocation)
            ? match.Groups["id"].Value
            : null;

    private async Task<StoreGame?> GameAsync(string id, UninstallEntry entry, CancellationToken cancellationToken)
    {
        string folder = entry.InstallLocation!;
        GogInfo? info = await GogInfo.ReadAsync(folder, id, cancellationToken);
        if (info is { IsGame: false })
        {
            return null;
        }

        string name = GameName(id) ?? StoreFiles.NonEmpty(entry.DisplayName) ?? StoreFiles.NonEmpty(info?.Name) ?? StoreFiles.FolderName(folder);
        return new(name, folder, GameSource.Gog, StoreFiles.ExistingExecutable(folder, info?.PrimaryExecutable));
    }

    // GOG Galaxy's own name for the game, from either registry view.
    private string? GameName(string id) =>
        StoreFiles.NonEmpty(registry.ReadString(RegistryRoot.LocalMachine, RegistryView.Registry32, $@"{GamesKey}\{id}", "gameName"))
        ?? StoreFiles.NonEmpty(registry.ReadString(RegistryRoot.LocalMachine, RegistryView.Registry64, $@"{GamesKey}\{id}", "gameName"));

    [GeneratedRegex(@"^(?<id>\d+)_is1$", RegexOptions.CultureInvariant)]
    private static partial Regex GogKey();
}
