using Autopilot.Core.Platform;

namespace Autopilot.Core.Library.Stores;

/// <summary>One program in Windows' Add/Remove Programs list, as its uninstall key says.</summary>
internal sealed record UninstallEntry(string KeyName, string? DisplayName, string? InstallLocation, string? Publisher, string? UninstallString)
{
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    private static readonly RegistryView[] _views = [RegistryView.Registry64, RegistryView.Registry32];

    private static readonly RegistryRoot[] _roots = [RegistryRoot.LocalMachine, RegistryRoot.CurrentUser];

    /// <summary>Every uninstall key of both registry views, for all users and for the current one.</summary>
    public static IEnumerable<UninstallEntry> ReadAll(IRegistry registry)
    {
        foreach (RegistryView view in _views)
        {
            foreach (RegistryRoot root in _roots)
            {
                foreach (string name in registry.SubKeyNames(root, view, UninstallKey))
                {
                    string key = $@"{UninstallKey}\{name}";
                    yield return new(
                        name,
                        registry.ReadString(root, view, key, "DisplayName"),
                        registry.ReadString(root, view, key, "InstallLocation"),
                        registry.ReadString(root, view, key, "Publisher"),
                        registry.ReadString(root, view, key, "UninstallString"));
                }
            }
        }
    }
}
