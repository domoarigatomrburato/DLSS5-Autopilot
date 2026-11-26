using Autopilot.Core.Executables;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Graphics;

/// <summary>
/// A 32-bit Unreal Engine 3 game: <c>&lt;root&gt;\Binaries\Win32\&lt;game&gt;.exe</c> beside a project
/// folder holding a <c>CookedPC*</c> folder. It names d3d11.dll for the renderer it can switch to
/// and draws with Direct3D 9 unless its config sets <c>AllowD3D11=True</c>.
/// </summary>
internal static class Unreal3
{
    /// <summary>Entries listed at most per folder: the root, each project, <c>My Games</c>.</summary>
    public const int MaxEntries = 60;

    /// <summary>Ini files read at most per config folder.</summary>
    public const int MaxIniFiles = 10;

    /// <summary>Characters of each ini file looked at.</summary>
    public const int MaxCharacters = 400_000;

    private const string Key = "AllowD3D11";

    public static async Task<ApiDecision?> DecideAsync(GameFolder game, CancellationToken cancellationToken)
    {
        if (game.Bitness != Bitness.Bits32 || Root(game) is not { } root)
        {
            return null;
        }

        List<(string Project, string Cooked)> projects = [.. FolderListing.Folders(root, MaxEntries, cancellationToken)
            .Select(project => (project, Cooked: CookedFolder(project, cancellationToken)))
            .Where(project => project.Cooked is not null)
            .Select(project => (project.project, project.Cooked!))];
        if (projects.Count == 0)
        {
            return null;
        }

        foreach ((string project, _) in projects)
        {
            foreach (string ini in PlayerInis(game, root, project, cancellationToken).Concat(EngineInis(Path.Join(project, "Config"), cancellationToken)))
            {
                if (await AllowsD3D11Async(ini, cancellationToken) is { } allows)
                {
                    return allows
                        ? new ApiDecision(GraphicsApi.DX11, ApiReason.Unreal3, Texts.Format("ApiUnreal3Dx11", ini))
                        : new ApiDecision(GraphicsApi.DX9, ApiReason.Unreal3, Texts.Format("ApiUnreal3Dx9Set", ini));
                }
            }
        }

        return new ApiDecision(GraphicsApi.DX9, ApiReason.Unreal3, Texts.Format("ApiUnreal3Dx9", Path.Join(Path.GetFileName(projects[0].Project), projects[0].Cooked)));
    }

    private static string? Root(GameFolder game)
    {
        DirectoryInfo? binaries = Directory.GetParent(game.Folder);
        return string.Equals(Path.GetFileName(game.Folder), "Win32", StringComparison.OrdinalIgnoreCase)
            && string.Equals(binaries?.Name, "Binaries", StringComparison.OrdinalIgnoreCase)
            ? binaries?.Parent?.FullName
            : null;
    }

    private static string? CookedFolder(string project, CancellationToken cancellationToken) =>
        FolderListing.Folders(project, MaxEntries, cancellationToken)
            .Select(Path.GetFileName)
            .FirstOrDefault(folder => folder!.StartsWith("CookedPC", StringComparison.OrdinalIgnoreCase));

    // The player's copy under Documents\My Games\<studio or game>\<Project>\Config, counted only when
    // its studio or game folder is named like the game's own folder ("UDKGame" is every UDK game's name).
    private static IEnumerable<string> PlayerInis(GameFolder game, string root, string project, CancellationToken cancellationToken)
    {
        string key = Letters(Path.GetFileName(root));
        string projectName = Path.GetFileName(project);
        return game.Search.DocumentsFolders.SelectMany(documents =>
            FolderListing.Folders(Path.Join(documents, "My Games"), MaxEntries, cancellationToken)
                .Where(owner => NamedLike(key, Letters(Path.GetFileName(owner))))
                .SelectMany(owner => EngineInis(Path.Join(owner, projectName, "Config"), cancellationToken))
                .Take(MaxIniFiles));
    }

    private static IEnumerable<string> EngineInis(string config, CancellationToken cancellationToken) =>
        FolderListing.Files(config, cancellationToken)
            .Where(path => path.EndsWith("Engine.ini", StringComparison.OrdinalIgnoreCase))
            .Take(MaxIniFiles);

    private static bool NamedLike(string key, string owner) =>
        key.Length > 0 && owner.Length > 0 && (owner.Contains(key, StringComparison.Ordinal) || key.Contains(owner, StringComparison.Ordinal));

    private static string Letters(string name) => string.Concat(name.Where(char.IsAsciiLetterOrDigit)).ToUpperInvariant();

    // The first AllowD3D11 line with a true or false value decides; null when the file says neither.
    private static async Task<bool?> AllowsD3D11Async(string ini, CancellationToken cancellationToken)
    {
        string? text = await IniFile.ReadAsync(ini, cancellationToken);
        if (text is null)
        {
            return null;
        }

        IniEntry? said = IniFile.Entries(text.Length > MaxCharacters ? text[..MaxCharacters] : text)
            .FirstOrDefault(entry => string.Equals(entry.Key, Key, StringComparison.OrdinalIgnoreCase) && bool.TryParse(entry.Value, out _));
        return said is null ? null : bool.Parse(said.Value);
    }
}
