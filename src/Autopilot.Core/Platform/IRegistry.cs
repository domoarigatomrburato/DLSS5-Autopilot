namespace Autopilot.Core.Platform;

/// <summary>Reads the Windows registry. Nothing it returns is trusted: every value is whatever a program wrote.</summary>
public interface IRegistry
{
    /// <summary>The names of the subkeys of a key; empty when the key is missing or cannot be opened.</summary>
    IReadOnlyList<string> SubKeyNames(RegistryRoot root, RegistryView view, string path);

    /// <summary>A string value of a key; null when the key or the value is missing, unreadable or not a string.</summary>
    string? ReadString(RegistryRoot root, RegistryView view, string path, string name);

    /// <summary>The names of the values of a key; empty when the key is missing or cannot be opened.</summary>
    IReadOnlyList<string> ValueNames(RegistryRoot root, RegistryView view, string path);

    /// <summary>A DWORD value of a key; null when the key or the value is missing, unreadable or not a DWORD.</summary>
    int? ReadDword(RegistryRoot root, RegistryView view, string path, string name);
}
