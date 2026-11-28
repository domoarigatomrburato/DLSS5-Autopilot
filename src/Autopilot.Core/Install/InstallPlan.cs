using System.Buffers;
using System.IO.Enumeration;
using System.Security.Cryptography;
using Autopilot.Core.Gpu;
using Autopilot.Core.Routes;

namespace Autopilot.Core.Install;

/// <summary>
/// What an install wants, as data, built without touching disk: the record's header and the groups of
/// file steps, in order. Every path is relative to <see cref="GameFolder"/>, with either slash.
/// </summary>
/// <param name="GameFolder">The game folder: the boundary of everything the install touches.</param>
/// <param name="InstallFolder">The folder holding the game's executable and the record, at or below <paramref name="GameFolder"/>.</param>
/// <param name="Header">What the record says about the install as a whole.</param>
/// <param name="Groups">The steps, one group per component or route-specific step, in the order they run.</param>
internal sealed record InstallPlan(string GameFolder, string InstallFolder, PlanHeader Header, IReadOnlyList<PlanGroup> Groups)
{
    /// <summary>
    /// Whether a step is still a pattern, or places a file whose bytes are not gathered yet (<see cref="PendingSource"/>):
    /// such a plan is a preview's, and the transaction refuses to run it.
    /// </summary>
    public bool NeedsGathering => Groups.Any(group => group.Steps.Any(step => step.NeedsGathering));
}

/// <summary>What the record says about an install as a whole.</summary>
/// <param name="ToolVersion">The build installing.</param>
/// <param name="Game">The game installed for.</param>
/// <param name="Route">The route installed.</param>
/// <param name="Choices">The person's settings the install read: every choice needed to repeat it.</param>
/// <param name="Components">Each component's id and the version installed.</param>
/// <param name="Notes">What the install says, for the report.</param>
/// <param name="Warnings">The warnings it gives.</param>
/// <param name="Skipped">What it skips.</param>
internal sealed record PlanHeader(
    string ToolVersion,
    RecordedGame Game,
    RouteId Route,
    RouteSettings Choices,
    IReadOnlyDictionary<string, string> Components,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Skipped);

/// <summary>One group of steps: what progress names as one step, journaled before any of it changes the disk.</summary>
/// <param name="Name">The step's name, as progress shows it.</param>
/// <param name="Steps">The file steps, in order.</param>
internal sealed record PlanGroup(string Name, IReadOnlyList<PlanStep> Steps);

/// <summary>
/// One thing an install wants at a path. A closed set: each kind is resolved by <see cref="InstallResolver"/>
/// into the actions that get there; a new kind adds its record here and its case there.
/// </summary>
/// <param name="Path">The path, relative to the game folder.</param>
internal abstract record PlanStep(string Path)
{
    /// <summary>The step's path, full, below <paramref name="paths"/>' game folder.</summary>
    /// <exception cref="Archives.UnsafePathException">The path is absolute, climbs out, or is not a valid Windows name.</exception>
    public virtual string FullIn(InstallPaths paths) => paths.Full(Path);

    /// <summary>
    /// The key a change outside the game folder's entries (a layer, a program's graphics preference) is named once under in a
    /// plan, apart from the entries' paths; null for a step of an entry, named once by its full path.
    /// </summary>
    /// <exception cref="Archives.UnsafePathException">The path is absolute, climbs out, or is not a valid Windows name.</exception>
    public virtual string? SharedKeyIn(InstallPaths paths) => null;

    /// <summary>Whether the step names a path of the game folder, which the checks keep inside it; a layer names none.</summary>
    public virtual bool NamesPath => true;

    /// <summary>Whether the step's files are not gathered yet (a pattern, a pending file): only the preview reads it.</summary>
    public virtual bool NeedsGathering => false;
}

/// <summary>A file placed at <paramref name="Path"/>, whose bytes are known and verified before the install starts.</summary>
/// <param name="Path">Where it goes.</param>
/// <param name="Class">Whether it stays as written.</param>
/// <param name="Facts">The hash and size its bytes must have.</param>
/// <param name="Source">Where its bytes come from.</param>
internal sealed record PlaceFile(string Path, EntryClass Class, FileFacts Facts, FileSource Source) : PlanStep(Path)
{
    public override bool NeedsGathering => Source is PendingSource;
}

/// <summary>
/// A file that must not be under its name while the install stands: the game's <c>d3dcompiler_47.dll</c>
/// (<see cref="BackupKind.Dlss5Off"/>), or an add-on or proxy of another route nobody recorded (<see cref="BackupKind.Backup"/>).
/// Nothing at the path, nothing to do.
/// </summary>
/// <param name="Path">The file.</param>
/// <param name="Backup">The name it is kept under.</param>
internal sealed record MoveAside(string Path, BackupKind Backup) : PlanStep(Path);

/// <summary>A folder the install needs; one already there is not recorded as created.</summary>
/// <param name="Path">The folder.</param>
internal sealed record CreateFolder(string Path) : PlanStep(Path);

/// <summary>
/// An entry of the previous install left as it is (a runtime an update keeps). Every previous entry the plan
/// neither places, moves aside nor keeps is held and deleted at commit. The previous record must list the path.
/// </summary>
/// <param name="Path">The entry's path.</param>
internal sealed record KeepEntry(string Path) : PlanStep(Path);

/// <summary>
/// Files known only by a pattern until a download reveals them (a shader pack, an OptiScaler package not in the cache):
/// the preview shows them as a pattern line. A plan holding one is never run: once gathered, each becomes the
/// <see cref="PlaceFile"/> steps of the real list.
/// </summary>
/// <param name="Path">
/// The folder the files go into and their names' pattern, relative to the game folder
/// (<c>reshade-shaders/Shaders/lumenite_*.fxh</c>; <c>*</c> for "the rest of the package"); the pattern is the last part.
/// </param>
internal sealed record PlacePattern(string Path) : PlanStep(Path)
{
    public override bool NeedsGathering => true;

    /// <summary>The folder checked as any step's path, the pattern joined to it as it is, since <c>*</c> and <c>?</c> are no name's characters.</summary>
    public override string FullIn(InstallPaths paths)
    {
        int last = Path.LastIndexOfAny(['/', '\\']);
        string folder = last < 0 ? paths.GameFolder : paths.Full(Path[..last]);
        return System.IO.Path.Join(folder, Path[(last + 1)..]);
    }

    /// <summary>Whether the file at <paramref name="full"/> is one <paramref name="pattern"/> (a full pattern, from <see cref="FullIn"/>) stands for: in its folder, its name matching.</summary>
    public static bool Matches(string pattern, string full) =>
        string.Equals(System.IO.Path.GetDirectoryName(pattern), System.IO.Path.GetDirectoryName(full), StringComparison.OrdinalIgnoreCase)
        && FileSystemName.MatchesSimpleExpression(System.IO.Path.GetFileName(pattern), System.IO.Path.GetFileName(full));
}

/// <summary>
/// A shared layer this install needs, registered for the current user unless a usable one is: its files go into
/// the layer folder outside the game folder, the one place outside it a plan writes files. Reused, refreshed or
/// registered as <see cref="InstallResolver"/> decides; the OpenXR layer only for a 64-bit game.
/// </summary>
/// <param name="Layer">Which layer.</param>
/// <param name="LibraryFacts">The DLL's hash and size.</param>
/// <param name="Library">The DLL's bytes, from ReShade's setup.</param>
/// <param name="Manifest">The manifest as ReShade's setup holds it; its <c>library_path</c> is rewritten.</param>
/// <remarks>Its path is empty: a layer names no path of the game folder.</remarks>
internal sealed record RegisterLayer(RegisteredLayer Layer, FileFacts LibraryFacts, FileSource Library, ReadOnlyMemory<byte> Manifest)
    : PlanStep(string.Empty)
{
    public override bool NamesPath => false;

    public override bool NeedsGathering => Library is PendingSource;

    public override string SharedKeyIn(InstallPaths paths) => $"layer\n{Layer}";
}

/// <summary>
/// Windows' graphics preference set to "High performance" for a program in the game folder, so it draws on the
/// NVIDIA card; a choice the person already made is kept. The plan names one only on a PC with an NVIDIA card and
/// another GPU, with the option on: see <see cref="GraphicsPreferenceSteps"/>.
/// </summary>
/// <param name="Path">The program, relative to the game folder.</param>
/// <param name="Other">The other GPU, which the program is kept off.</param>
internal sealed record SetGraphicsPreference(string Path, GpuVendor Other) : PlanStep(Path)
{
    public override string SharedKeyIn(InstallPaths paths) => $"preference\n{FullIn(paths)}";
}

/// <summary>The graphics preference steps of a plan.</summary>
internal static class GraphicsPreferenceSteps
{
    /// <summary>
    /// One step per program of <paramref name="programs"/> (the game's executable, and the helpers the route runs) when
    /// <paramref name="gpu"/> has another GPU beside the NVIDIA card and the option is on; none otherwise.
    /// </summary>
    public static IReadOnlyList<PlanStep> Of(GpuFacts gpu, bool optionOn, params IEnumerable<string> programs)
    {
        ArgumentNullException.ThrowIfNull(gpu);
        return optionOn && gpu.OtherGpu is { } other ? [.. programs.Select(program => new SetGraphicsPreference(program, other))] : [];
    }
}

/// <summary>Where a placed file's bytes come from.</summary>
internal abstract record FileSource
{
    private const int BufferSize = 1024 * 1024;

    /// <summary>Writes the bytes to <paramref name="target"/>, hashing them on the way: the facts of what was written.</summary>
    /// <exception cref="IOException">The source could not be read, or the target written.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public async Task<FileFacts> CopyToAsync(Stream target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        await using Stream source = Open();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        long size = 0;
        try
        {
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                size += read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return new(Convert.ToHexStringLower(hash.GetHashAndReset()), size);
    }

    /// <summary>Opens the bytes for reading.</summary>
    protected abstract Stream Open();
}

/// <summary>A verified file in the download cache.</summary>
/// <param name="Path">Its full path.</param>
internal sealed record CachedSource(string Path) : FileSource
{
    protected override Stream Open() =>
        new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.Asynchronous | FileOptions.SequentialScan);
}

/// <summary>Bytes the tool generates (a config, a shader header).</summary>
/// <param name="Bytes">The bytes.</param>
internal sealed record GeneratedSource(ReadOnlyMemory<byte> Bytes) : FileSource
{
    protected override Stream Open() => new MemoryStream(Bytes.ToArray(), writable: false);
}

/// <summary>
/// Bytes a preview names and does not have: the file is known by name only until the gather downloads it. Its facts
/// are <see cref="Facts"/>, and opening it is a bug: a plan holding one is refused by the transaction.
/// </summary>
internal sealed record PendingSource : FileSource
{
    /// <summary>The one pending source.</summary>
    public static PendingSource Instance { get; } = new();

    /// <summary>The facts of a file not gathered yet: no hash, and no size, so the preview's free-space check counts only known sizes.</summary>
    public static FileFacts Facts { get; } = new("", 0);

    /// <exception cref="InvalidOperationException">Always: a pending file has no bytes.</exception>
    protected override Stream Open() => throw new InvalidOperationException("a pending file has no bytes: gather the plan's files first");
}
