namespace Autopilot.Core.Library.Protection;

/// <summary>
/// The anti-cheat markers, one row per product in the order of library.md's table: the first row
/// whose marker is in a name gives its product. Product names are proper names, so they are data.
/// </summary>
internal static class AntiCheatMarkers
{
    private sealed record Row(string Product, params string[] Markers);

    private static readonly Row[] _table =
    [
        new("BattlEye", "beservice", "beclient", "battleye"),
        new("Easy Anti-Cheat", "easyanticheat", "eac_launcher"),
        // Vanguard installs under Program Files, never into a game folder: only its two files count, never the word.
        new("Riot Vanguard", "vgk.sys", "vgc.exe"),
        new("nProtect GameGuard", "gameguard"),
        new("XIGNCODE3", "xigncode"),
        // Denuvo alone is copy protection most single-player games carry; only its anti-cheat is.
        new("Denuvo Anti-Cheat", "denuvo-anti-cheat", "denuvoanticheat"),
        new("PunkBuster", "punkbuster"),
        new("FACEIT AC", "faceit"),
        // Activision's PC titles since Warzone/Vanguard start from cod.exe in the root and carry no file named for the anti-cheat.
        new("Ricochet", "ricochet", "cod.exe"),
        new("EA Javelin", "eaanticheat", "ea_anticheat"),
        // The driver installs system-wide, so the executable's name is the evidence.
        new("HoYoverse anti-cheat", "zenlesszonezero", "genshinimpact", "starrail", "mhyprot"),
        // A folder beside the game with its installer in it, so the folder's name is the evidence.
        new("Elytra Anti-Cheat", "elytra"),
        // Arknights: Endfield carries it with nothing named after it, so the executable's name is the evidence.
        new("ACE (Anti-Cheat Expert)", "ace-base", "anticheatexpert", "endfield"),
    ];

    /// <summary>The product a file or folder name belongs to, or null.</summary>
    public static string? ProductOf(string name) =>
        _table.FirstOrDefault(row => row.Markers.Any(marker => Matches(name, marker)))?.Product;

    // A marker that is a whole file name (vgk.sys, cod.exe) matches only that name. Any other is a
    // whole word of the name: no letter or digit before it, none after it, digits aside (mhyprot3.sys).
    private static bool Matches(string name, string marker)
    {
        if (marker.EndsWith(".sys", StringComparison.Ordinal) || marker.EndsWith(".exe", StringComparison.Ordinal))
        {
            return name.Equals(marker, StringComparison.OrdinalIgnoreCase);
        }

        return WholeWord.In(name, marker, digitsAfter: true);
    }
}
