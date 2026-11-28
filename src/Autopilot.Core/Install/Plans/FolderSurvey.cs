using Autopilot.Core.Executables;
using Autopilot.Core.Graphics;
using Microsoft.Extensions.Logging.Abstractions;

namespace Autopilot.Core.Install.Plans;

/// <summary>What a file under one of ReShade's proxy names carries, when it is not the previous install's.</summary>
internal enum ProxyContent
{
    /// <summary>ReShade.</summary>
    ReShade,

    /// <summary>OptiScaler installed by hand.</summary>
    OptiScaler,

    /// <summary>DXVK.</summary>
    Dxvk,

    /// <summary>REFramework.</summary>
    Reframework,

    /// <summary>Special K, which can load ReShade itself.</summary>
    SpecialK,

    /// <summary>Anything else: the game's own file, Special K, another injector.</summary>
    Other,
}

/// <summary>A file the survey found: whether the previous install wrote it (and it is unchanged), and for a proxy name what it carries.</summary>
/// <param name="Ours">Listed as written by the previous install, its content as written.</param>
/// <param name="Content">For a ReShade proxy name not <paramref name="Ours"/>, what the file is; null for any other name, and for ours.</param>
internal sealed record SurveyedFile(bool Ours, ProxyContent? Content);

/// <summary>What the survey reads, and where.</summary>
/// <param name="GameFolder">The game folder, full.</param>
/// <param name="InstallFolder">The install folder (the executable's), full.</param>
/// <param name="Recorded">The folder's record as <see cref="InstallRecords"/> found it; null when there is none.</param>
/// <param name="SystemFolder">Windows' <c>System32</c> folder, whose <c>d3dcompiler_47.dll</c> the game's is compared with.</param>
internal sealed record SurveyRequest(string GameFolder, string InstallFolder, RecordedInstall? Recorded, string SystemFolder);

/// <summary>
/// Everything the route plans read from the game folder, in one pass, so that <see cref="RoutePlans"/> is a pure function:
/// which of the names a plan may touch are there and whose they are, the config files' bytes, and the game's and Windows'
/// compiler versions. Reads only: writes nothing, requests nothing. Every path is relative to the game folder, <c>/</c>
/// separated. Where the game keeps its own DLSS runtimes is in the route facts, not here.
/// </summary>
internal sealed record FolderSurvey
{
    // The names looked at beside the executable, and whether each is one ReShade loads as, whose content is read.
    private static readonly (string Name, bool Proxy)[] _looked =
    [
        .. PlanNames.ReShadeProxies.Select(name => (name, true)),
        .. PlanNames.AllAddOns.Concat(PlanNames.OptiScalerCompanions).Append(KnownFiles.Compiler).Select(name => (name, false)),
    ];

    // The config files beside the executable whose bytes a plan edits.
    private static readonly string[] _configs = [PlanNames.ReShadeIni];

    /// <summary>The names looked at that are there, and what each is.</summary>
    public required IReadOnlyDictionary<string, SurveyedFile> Present { get; init; }

    /// <summary>The paths the previous install keeps moved aside (a backup behind the path), which a plan keeps aside by naming them again.</summary>
    public required IReadOnlySet<string> MovedAside { get; init; }

    /// <summary>Every path the previous install's record lists.</summary>
    public required IReadOnlySet<string> Previous { get; init; }

    /// <summary>Each component's id and version as the previous install's record lists them; empty on a fresh install.</summary>
    public required IReadOnlyDictionary<string, string> PreviousComponents { get; init; }

    /// <summary>The install folder, relative to the game folder: empty when they are one.</summary>
    public required string InstallFolder { get; init; }

    /// <summary>The bytes of each config file that is there, by path.</summary>
    public required IReadOnlyDictionary<string, ReadOnlyMemory<byte>> Contents { get; init; }

    /// <summary>The file version of the game's <c>d3dcompiler_47.dll</c>; null when it has none there or the version cannot be read.</summary>
    public required string? CompilerVersion { get; init; }

    /// <summary>The file version of Windows' own <c>d3dcompiler_47.dll</c>; null when it cannot be read.</summary>
    public required string? SystemCompilerVersion { get; init; }

    /// <summary>The path of <paramref name="name"/> beside the executable.</summary>
    public string Beside(string name) => Join(InstallFolder, name);

    /// <summary>Whether a plan moves <paramref name="path"/> aside: a file there that is not ours, or one the previous install keeps aside.</summary>
    public bool KeptAside(string path) => At(path) is { Ours: false } || (At(path) is null && MovedAside.Contains(path));

    /// <summary>What is at <paramref name="path"/>; null when nothing the survey looked at is there.</summary>
    public SurveyedFile? At(string path) => Present.GetValueOrDefault(path);

    /// <summary>The bytes of the config file at <paramref name="path"/>; null when there is none.</summary>
    public ReadOnlyMemory<byte>? Content(string path) => Contents.TryGetValue(path, out ReadOnlyMemory<byte> bytes) ? bytes : null;

    /// <summary>Reads the folder.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    /// <exception cref="IOException">A file the survey must read could not be read.</exception>
    /// <exception cref="UnauthorizedAccessException">A file the survey must read could not be opened.</exception>
    public static async Task<FolderSurvey> ReadAsync(SurveyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        InstallPaths paths = new(request.GameFolder);
        var prior = PriorInstall.Of(request.Recorded, paths, NullLogger.Instance);
        string install = paths.Relative(Path.GetFullPath(request.InstallFolder));
        Dictionary<string, SurveyedFile> present = await PresentAsync(paths, prior, install, cancellationToken);
        Dictionary<string, ReadOnlyMemory<byte>> contents = await ContentsAsync(paths, install, cancellationToken);
        string? compiler = await VersionAsync(paths.Full(Join(install, KnownFiles.Compiler)), cancellationToken);
        string? systemCompiler = await VersionAsync(Path.Join(request.SystemFolder, KnownFiles.Compiler), cancellationToken);
        return new()
        {
            Present = present,
            MovedAside = Keys(prior, movedAsideOnly: true),
            Previous = Keys(prior, movedAsideOnly: false),
            PreviousComponents = prior.Record?.Components ?? new Dictionary<string, string>(),
            InstallFolder = install,
            Contents = contents,
            CompilerVersion = compiler,
            SystemCompilerVersion = systemCompiler,
        };
    }

    // Every name looked at that is there: whether it is ours, and for a proxy not ours, what it carries.
    private static async Task<Dictionary<string, SurveyedFile>> PresentAsync(InstallPaths paths, PriorInstall prior, string install, CancellationToken cancellationToken)
    {
        Dictionary<string, SurveyedFile> present = [with(StringComparer.OrdinalIgnoreCase)];
        foreach ((string name, bool proxy) in _looked)
        {
            string relative = Join(install, name);
            string full = paths.Full(relative);
            if (File.Exists(full))
            {
                bool ours = await prior.IsOursAsync(prior.At(full), full, cancellationToken);
                present[relative] = new(ours, proxy && !ours ? await ContentAsync(full, cancellationToken) : null);
            }
        }

        return present;
    }

    // The config files that are there, read whole.
    private static async Task<Dictionary<string, ReadOnlyMemory<byte>>> ContentsAsync(InstallPaths paths, string install, CancellationToken cancellationToken)
    {
        Dictionary<string, ReadOnlyMemory<byte>> contents = [with(StringComparer.OrdinalIgnoreCase)];
        foreach (string relative in _configs.Select(name => Join(install, name)))
        {
            if (File.Exists(paths.Full(relative)))
            {
                contents[relative] = await File.ReadAllBytesAsync(paths.Full(relative), cancellationToken);
            }
        }

        return contents;
    }

    // What the file carries, in one read: OptiScaler and Special K DLLs may name ReShade, and ReShade names neither.
    private static async Task<ProxyContent> ContentAsync(string full, CancellationToken cancellationToken)
    {
        IReadOnlySet<string> marks = await ContentMarks.MarksAsync(full, cancellationToken);
        return marks.Contains(ContentMarks.OptiScaler) ? ProxyContent.OptiScaler
            : marks.Contains(ContentMarks.SpecialK) ? ProxyContent.SpecialK
            : marks.Contains(ContentMarks.ReShade) ? ProxyContent.ReShade
            : marks.Contains(ContentMarks.Dxvk) ? ProxyContent.Dxvk
            : marks.Contains(ContentMarks.Reframework) ? ProxyContent.Reframework
            : ProxyContent.Other;
    }

    // The version stamped in the file; null when there is none there, or it carries none or is not a Windows binary.
    private static async Task<string?> VersionAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return File.Exists(path) && await Executable.FileVersionAsync(path, cancellationToken) is { Length: > 0 } version ? version : null;
        }
        catch (PeException)
        {
            return null;
        }
    }

    private static string Join(string folder, string name) => folder.Length == 0 ? name : folder + "/" + name;

    // The paths the previous install's record lists, or only those it keeps moved aside.
    private static HashSet<string> Keys(PriorInstall prior, bool movedAsideOnly) =>
    [
        with(StringComparer.OrdinalIgnoreCase),
        .. prior.Entries.Select(entry => entry.Value.Entry).Where(entry => !movedAsideOnly || entry.State == EntryState.MovedAside).Select(entry => RecordPaths.Key(entry.Path)),
    ];
}
