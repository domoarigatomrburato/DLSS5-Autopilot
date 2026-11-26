namespace Autopilot.Core.Platform;

/// <summary>
/// Which half of a 64-bit Windows registry a read sees: <see cref="Registry32"/> is where 32-bit
/// programs write, the <c>WOW6432Node</c> keys under <c>SOFTWARE</c>.
/// </summary>
public enum RegistryView
{
    Registry64,
    Registry32,
}
