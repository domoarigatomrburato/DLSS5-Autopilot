using Autopilot.Core.Platform;

namespace Autopilot.Core.Library.Stores;

/// <summary>
/// One program in Windows' Add/Remove Programs list, as its uninstall key says. Each value is read
/// on first use, so a filter on the key name or one value reads nothing else.
/// </summary>
internal sealed class UninstallEntry
{
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    private static readonly RegistryView[] _views = [RegistryView.Registry64, RegistryView.Registry32];

    private static readonly RegistryRoot[] _roots = [RegistryRoot.LocalMachine, RegistryRoot.CurrentUser];

    private readonly IRegistry _registry;
    private readonly RegistryRoot _root;
    private readonly RegistryView _view;
    private readonly string _key;
    private readonly Dictionary<string, string?> _values = [];

    private UninstallEntry(IRegistry registry, RegistryRoot root, RegistryView view, string keyName)
    {
        _registry = registry;
        _root = root;
        _view = view;
        _key = $@"{UninstallKey}\{keyName}";
        KeyName = keyName;
    }

    public string KeyName { get; }

    public string? DisplayName => Value("DisplayName");

    public string? InstallLocation => Value("InstallLocation");

    public string? Publisher => Value("Publisher");

    public string? UninstallString => Value("UninstallString");

    /// <summary>Every uninstall key of both registry views, for all users and for the current one.</summary>
    public static IEnumerable<UninstallEntry> ReadAll(IRegistry registry)
    {
        foreach (RegistryView view in _views)
        {
            foreach (RegistryRoot root in _roots)
            {
                foreach (string name in registry.SubKeyNames(root, view, UninstallKey))
                {
                    yield return new(registry, root, view, name);
                }
            }
        }
    }

    /// <summary>The games of one store: the entries <paramref name="isGame"/> picks whose install folder is there, named as the entry is.</summary>
    public static Task<IReadOnlyList<StoreGame>> GamesAsync(IRegistry registry, Func<UninstallEntry, bool> isGame, GameSource source, CancellationToken cancellationToken)
    {
        FoundGames found = new(source);
        foreach (UninstallEntry entry in ReadAll(registry))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (isGame(entry))
            {
                found.Add(entry.DisplayName, entry.InstallLocation);
            }
        }

        return Task.FromResult(found.Games);
    }

    private string? Value(string name)
    {
        if (!_values.TryGetValue(name, out string? value))
        {
            value = _registry.ReadString(_root, _view, _key, name);
            _values[name] = value;
        }

        return value;
    }
}
