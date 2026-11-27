using System.Globalization;
using Autopilot.Core.Archives;
using Autopilot.Core.Graphics;
using Autopilot.Core.Platform;

namespace Autopilot.Core.Install;

/// <summary>
/// The places one install touches, by its game folder: recorded paths to full ones and back, the names a
/// file's backup, held copy and superseded backup take beside it, and whether a place stays inside the
/// game folder once junctions and symbolic links are followed.
/// </summary>
internal sealed class InstallPaths
{
    private readonly Lazy<string> _resolvedRoot;

    /// <param name="gameFolder">The game folder every recorded path is relative to.</param>
    public InstallPaths(string gameFolder)
    {
        GameFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameFolder));
        _resolvedRoot = new(() => ResolvedPath.Of(GameFolder));
    }

    /// <summary>The game folder, full, without a trailing separator.</summary>
    public string GameFolder { get; }

    /// <summary>The full path of a recorded or planned path, normalized: the one spelling paths are compared in.</summary>
    /// <exception cref="UnsafePathException">The path is absolute, climbs out, or is not a valid Windows name.</exception>
    public string Full(string relative) => Path.GetFullPath(SafePath.Join(GameFolder, relative));

    /// <summary>The recorded form of a full path below the game folder: <c>/</c> separated; <c>""</c> for the game folder itself.</summary>
    public string Relative(string full) => RecordPaths.KeyBelow(GameFolder, full) ?? "";

    /// <summary>Whether <paramref name="full"/> stays at or below the game folder once every link along it is followed.</summary>
    public bool Contains(string full) => PathParts.IsAtOrBelow(_resolvedRoot.Value, ResolvedPath.Of(full));

    /// <summary>The name a file of the previous install is held under until commit.</summary>
    public static string HeldOf(string full) => full + KnownFiles.HeldSuffix;

    /// <summary>The name <paramref name="kind"/> keeps a game's original under, beside it.</summary>
    public static string BackupOf(string full, BackupKind kind) => full + OriginalFile.SuffixOf(kind);

    /// <summary>
    /// The superseded name number <paramref name="number"/> of <paramref name="full"/>, counted from 1:
    /// <c>.invidia-claritatis-superseded</c>, then <c>.2</c>, <c>.3</c> ... appended.
    /// </summary>
    public static string SupersededOf(string full, int number) => Numbered(full, KnownFiles.SupersededSuffix, number);

    /// <summary>
    /// The name number <paramref name="number"/> of <paramref name="full"/> with <paramref name="suffix"/>, counted from 1:
    /// the suffix alone, then <c>.2</c>, <c>.3</c> ... appended, for a file kept aside beside earlier ones.
    /// </summary>
    public static string Numbered(string full, string suffix, int number) =>
        number == 1 ? full + suffix : string.Create(CultureInfo.InvariantCulture, $"{full}{suffix}.{number}");

    /// <summary>The first name <see cref="Numbered"/> gives for <paramref name="full"/> and <paramref name="suffix"/> that is free on disk.</summary>
    public static string FirstFree(string full, string suffix)
    {
        for (int number = 1; ; number++)
        {
            string name = Numbered(full, suffix, number);
            if (!File.Exists(name))
            {
                return name;
            }
        }
    }

    /// <summary>The game's file at <paramref name="full"/>, whose content is <paramref name="facts"/>, as its backup records it.</summary>
    public static OriginalFile Original(string full, FileFacts facts, BackupKind kind)
    {
        FileInfo file = new(full);
        return new(facts, file.Attributes, file.LastWriteTimeUtc, kind);
    }
}

/// <summary>The superseded names one run gives out: each free on disk and not given out before in the run.</summary>
internal sealed class SupersededNames
{
    private readonly HashSet<string> _given = [with(StringComparer.OrdinalIgnoreCase)];

    /// <summary>The first free superseded name of <paramref name="full"/>, and the report's note saying the backup went there.</summary>
    /// <param name="full">The file whose backup is superseded.</param>
    /// <param name="relative">The file as the report names it.</param>
    public SupersededName Pick(string full, string relative)
    {
        for (int number = 1; ; number++)
        {
            string name = InstallPaths.SupersededOf(full, number);
            if (!File.Exists(name) && _given.Add(name))
            {
                return new(number, name, Note(relative, name));
            }
        }
    }

    /// <summary>
    /// The superseded name of <paramref name="full"/> already holding the original <paramref name="isOriginal"/>
    /// recognises, set aside by an earlier run, and its note; null when none does.
    /// </summary>
    /// <param name="full">The file whose backup was superseded.</param>
    /// <param name="relative">The file as the report names it.</param>
    /// <param name="isOriginal">Whether the file at a name holds the original.</param>
    public static async Task<SupersededName?> HoldingAsync(string full, string relative, Func<string, Task<bool>> isOriginal)
    {
        for (int number = 1; File.Exists(InstallPaths.SupersededOf(full, number)); number++)
        {
            string name = InstallPaths.SupersededOf(full, number);
            if (await isOriginal(name))
            {
                return new(number, name, Note(relative, name));
            }
        }

        return null;
    }

    // The report's note saying the old original went to name.
    private static string Note(string relative, string name) => Resources.Texts.Format("InstallSupersededNote", relative, Path.GetFileName(name));
}

/// <summary>A superseded name given out.</summary>
/// <param name="Number">Its number, counted from 1.</param>
/// <param name="Full">Its full path.</param>
/// <param name="Note">The report's note.</param>
internal sealed record SupersededName(int Number, string Full, string Note);
