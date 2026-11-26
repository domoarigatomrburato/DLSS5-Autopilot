using Autopilot.Core.Platform;

namespace Autopilot.Core.Tests.Library.Stores;

/// <summary>An in-memory registry: keys by hive, view and path, ignoring case as Windows does.</summary>
internal sealed class FakeRegistry : IRegistry
{
    private readonly Dictionary<string, Dictionary<string, object>> _keys = [with(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Sets a value, creating the key and every key above it.</summary>
    public FakeRegistry Set(RegistryRoot root, RegistryView view, string path, string name, object value)
    {
        Key(root, view, path)[name] = value;
        return this;
    }

    /// <summary>Sets a 64-bit view value of <c>HKLM</c>.</summary>
    public FakeRegistry Machine(string path, string name, object value) => Set(RegistryRoot.LocalMachine, RegistryView.Registry64, path, name, value);

    /// <summary>Sets a <c>WOW6432Node</c> value of <c>HKLM</c>: the 32-bit view.</summary>
    public FakeRegistry Machine32(string path, string name, object value) => Set(RegistryRoot.LocalMachine, RegistryView.Registry32, path, name, value);

    /// <summary>An Add/Remove Programs entry, with only the values given.</summary>
    public FakeRegistry Uninstall(string keyName, RegistryRoot root = RegistryRoot.LocalMachine, RegistryView view = RegistryView.Registry64, string? displayName = null, string? installLocation = null, string? publisher = null, string? uninstallString = null)
    {
        string path = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{keyName}";
        _ = Key(root, view, path);
        foreach ((string name, string? value) in new[] { ("DisplayName", displayName), ("InstallLocation", installLocation), ("Publisher", publisher), ("UninstallString", uninstallString) })
        {
            if (value is not null)
            {
                _ = Set(root, view, path, name, value);
            }
        }

        return this;
    }

    public IReadOnlyList<string> SubKeyNames(RegistryRoot root, RegistryView view, string path)
    {
        string prefix = Id(root, view, path) + "\\";
        return [.. _keys.Keys
            .Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !key[prefix.Length..].Contains('\\', StringComparison.Ordinal))
            .Select(key => key[prefix.Length..])];
    }

    public string? ReadString(RegistryRoot root, RegistryView view, string path, string name) =>
        _keys.TryGetValue(Id(root, view, path), out Dictionary<string, object>? values) && values.TryGetValue(name, out object? value) ? value as string : null;

    private Dictionary<string, object> Key(RegistryRoot root, RegistryView view, string path)
    {
        string[] parts = path.Split('\\');
        Dictionary<string, object> values = [];
        for (int depth = 1; depth <= parts.Length; depth++)
        {
            string id = Id(root, view, string.Join('\\', parts[..depth]));
            if (!_keys.TryGetValue(id, out values!))
            {
                values = [with(StringComparer.OrdinalIgnoreCase)];
                _keys[id] = values;
            }
        }

        return values;
    }

    private static string Id(RegistryRoot root, RegistryView view, string path) => $@"{root}\{view}\{path.Trim('\\')}";
}
