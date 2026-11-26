using System.Buffers;
using System.Collections.Frozen;

namespace Autopilot.Core.Archives;

/// <summary>
/// Reads names from archives and remote tags the way Windows reads them, on every host, so a name
/// that would escape its folder or open a device is refused before anything is written.
/// </summary>
public static class SafePath
{
    private static readonly SearchValues<char> _forbidden = SearchValues.Create("<>\"|?*");

    private static readonly char[] _separators = ['/', '\\'];

    // Windows also reserves the superscript digits after COM and LPT.
    private static readonly FrozenSet<string> _devices = new[] { "CON", "PRN", "AUX", "NUL" }
        .Concat(new[] { "COM", "LPT" }.SelectMany(stem => "123456789¹²³".Select(digit => $"{stem}{digit}")))
        .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Joins a validated relative name to <paramref name="root"/>, kept in the caller's spelling:
    /// nothing is resolved, so a root reached through a junction comes back as given.
    /// </summary>
    /// <exception cref="UnsafePathException">The name is absolute, climbs out, or is not a valid Windows name.</exception>
    public static string Join(string root, string relative) =>
        Path.Join(root, string.Join(Path.DirectorySeparatorChar, NonEmptyParts(relative)));

    /// <summary>Checks a single file name by the same rules, with no separators at all.</summary>
    /// <exception cref="UnsafePathException">The name is a path, a device or not a valid Windows name.</exception>
    public static void ValidateFileName(string name)
    {
        if (name.AsSpan().IndexOfAny(_separators) >= 0)
        {
            throw new UnsafePathException(name, "is a path, not a file name");
        }

        _ = NonEmptyParts(name);
    }

    /// <summary>The name's parts, with empty and "." parts dropped; empty for a name that is only the root.</summary>
    internal static string[] Parts(string name)
    {
        if (name.AsSpan().ContainsAny(_forbidden) || name.Any(char.IsControl))
        {
            throw new UnsafePathException(name, "holds a character Windows does not allow in a name");
        }

        if (name.StartsWith('/', StringComparison.Ordinal) || name.StartsWith('\\', StringComparison.Ordinal) || (name.Length >= 2 && char.IsAsciiLetter(name[0]) && name[1] == ':'))
        {
            throw new UnsafePathException(name, "is an absolute path");
        }

        string[] parts = [.. name.Split(_separators, StringSplitOptions.RemoveEmptyEntries).Where(part => part != ".")];
        foreach (string part in parts)
        {
            CheckPart(name, part);
        }

        return parts;
    }

    private static string[] NonEmptyParts(string name)
    {
        string[] parts = Parts(name);
        return parts.Length > 0 ? parts : throw new UnsafePathException(name, "is an empty name");
    }

    private static void CheckPart(string name, string part)
    {
        if (part == "..")
        {
            throw new UnsafePathException(name, "climbs out of its folder");
        }

        if (part.Contains(':', StringComparison.Ordinal))
        {
            throw new UnsafePathException(name, "names an alternate data stream");
        }

        if (part.EndsWith('.', StringComparison.Ordinal) || part.EndsWith(' ', StringComparison.Ordinal))
        {
            throw new UnsafePathException(name, "has a part ending in a dot or a space, which Windows drops");
        }

        int dot = part.IndexOf('.', StringComparison.Ordinal);
        string stem = (dot < 0 ? part : part[..dot]).TrimEnd(' ');
        if (_devices.Contains(stem))
        {
            throw new UnsafePathException(name, "is a reserved device name");
        }
    }
}
