using System.Text.RegularExpressions;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Library.Stores;

/// <summary>
/// Battle.net's installed games, from the uninstall keys whose uninstall command is Battle.net's with
/// a product <c>--uid</c>. Test and beta clients and the Battle.net client itself are left out.
/// </summary>
public sealed partial class BattleNetSource(IRegistry registry) : IGameSource
{
    public GameSource Source => GameSource.BattleNet;

    public Task<IReadOnlyList<StoreGame>> FindAsync(CancellationToken cancellationToken) =>
        UninstallEntry.GamesAsync(registry, IsGame, GameSource.BattleNet, cancellationToken);

    private static bool IsGame(UninstallEntry entry)
    {
        if (entry.UninstallString is null || BattleNetUid().Match(entry.UninstallString) is not { Success: true } match)
        {
            return false;
        }

        string displayName = entry.DisplayName?.Trim() ?? "";
        return !match.Groups["uid"].Value.Equals("battle.net", StringComparison.OrdinalIgnoreCase)
            && !entry.KeyName.Equals("Battle.net", StringComparison.OrdinalIgnoreCase)
            && !displayName.EndsWith("Test", StringComparison.OrdinalIgnoreCase)
            && !displayName.EndsWith("Beta", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("""Battle\.net.*--uid=(?<uid>[^\s"]+)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BattleNetUid();
}
