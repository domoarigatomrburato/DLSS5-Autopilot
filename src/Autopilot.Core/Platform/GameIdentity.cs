using System.Security.Cryptography;
using System.Text;

namespace Autopilot.Core.Platform;

/// <summary>
/// A game is its install folder (the folder holding the executable the tool installs beside),
/// normalised, so one folder reached two ways is one game. A game moved to another folder is a new game.
/// </summary>
public sealed record GameIdentity
{
    // Of the SHA-256's 64 hex digits, the key keeps the first 32.
    private const int KeyDigits = 32;

    private GameIdentity(string folder)
    {
        Folder = folder;
        Key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(folder.Replace('/', '\\').ToUpperInvariant())))[..KeyDigits];
    }

    /// <summary>
    /// The install folder in the host's form: the final path after junctions and symbolic links, with
    /// the host's separators (<c>\</c> on Windows) and no trailing separator. On Windows,
    /// <see cref="Path.GetFullPath(string)"/> (through <see cref="ResolvedPath"/>) also expands an
    /// 8.3 part (<c>PROGRA~1</c>) to its long name; that is checked on Windows in M7.
    /// </summary>
    public string Folder { get; }

    /// <summary>
    /// The per-game state key: the first 32 hex digits, upper case, of the SHA-256 of the UTF-8 of
    /// <see cref="Folder"/> with <c>\</c> separators, upper-cased invariantly, so it is the same on every host.
    /// </summary>
    public string Key { get; }

    /// <summary>The game whose install folder is <paramref name="installFolder"/>, spelled with either slash.</summary>
    /// <exception cref="ArgumentException">The folder is empty.</exception>
    public static GameIdentity Of(string installFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installFolder);
        string spelled = installFolder.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        return new(ResolvedPath.Of(spelled));
    }

    /// <summary>Two identities are one game when their folders are equal, ordinal, ignoring case.</summary>
    public bool Equals(GameIdentity? other) => other is not null && string.Equals(Folder, other.Folder, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Folder);
}
