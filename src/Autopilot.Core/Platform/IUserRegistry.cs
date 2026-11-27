namespace Autopilot.Core.Platform;

/// <summary>
/// Reads and writes the current user's registry (<c>HKEY_CURRENT_USER</c>, 64-bit view): the only
/// part of the registry the tool writes. Paths are below <c>HKEY_CURRENT_USER</c>. What it reads is
/// untrusted: every value is whatever a program wrote.
/// </summary>
/// <remarks>
/// A key that is not there reads as empty; a key that is there and cannot be read, and every write that
/// fails, throws what .NET's registry API throws: <see cref="IOException"/>,
/// <see cref="UnauthorizedAccessException"/> or <see cref="System.Security.SecurityException"/>.
/// </remarks>
public interface IUserRegistry
{
    /// <summary>The names of the values of the key at <paramref name="path"/>; empty when the key is missing.</summary>
    /// <exception cref="IOException">The key cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The key cannot be opened.</exception>
    /// <exception cref="System.Security.SecurityException">The key cannot be opened.</exception>
    IReadOnlyList<string> ValueNames(string path);

    /// <summary>A DWORD value; null when the key or the value is missing or the value is not a DWORD.</summary>
    /// <exception cref="IOException">The key cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The key cannot be opened.</exception>
    /// <exception cref="System.Security.SecurityException">The key cannot be opened.</exception>
    int? ReadDword(string path, string name);

    /// <summary>A string value; null when the key or the value is missing or the value is not a string.</summary>
    /// <exception cref="IOException">The key cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The key cannot be opened.</exception>
    /// <exception cref="System.Security.SecurityException">The key cannot be opened.</exception>
    string? ReadString(string path, string name);

    /// <summary>Sets a DWORD value, creating the key when it is missing.</summary>
    /// <exception cref="IOException">The value cannot be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The key cannot be written.</exception>
    /// <exception cref="System.Security.SecurityException">The key cannot be written.</exception>
    void SetDword(string path, string name, int value);

    /// <summary>Sets a string (<c>REG_SZ</c>) value, creating the key when it is missing.</summary>
    /// <exception cref="IOException">The value cannot be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The key cannot be written.</exception>
    /// <exception cref="System.Security.SecurityException">The key cannot be written.</exception>
    void SetString(string path, string name, string value);

    /// <summary>Deletes a value; one that is not there, or a key that is not there, is deleted already.</summary>
    /// <exception cref="IOException">The value cannot be deleted.</exception>
    /// <exception cref="UnauthorizedAccessException">The key cannot be written.</exception>
    /// <exception cref="System.Security.SecurityException">The key cannot be written.</exception>
    void DeleteValue(string path, string name);
}
