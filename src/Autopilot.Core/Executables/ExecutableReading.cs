namespace Autopilot.Core.Executables;

/// <summary>What reading a game's executable found: its tables, or that Windows refused its content.</summary>
internal abstract record ExecutableReading
{
    internal ExecutableReading()
    {
    }
}

/// <summary>
/// An executable whose headers and import tables were read. Import names are lower-case and the
/// sets compare whole names, ignoring case.
/// </summary>
internal sealed record ReadableExecutable(Bitness Bitness, IReadOnlySet<string> Imports, IReadOnlySet<string> DelayImports) : ExecutableReading;

/// <summary>
/// An executable that is listed in its folder but whose content Windows refuses to read (access
/// denied), as with Xbox and Game Pass games under <c>WindowsApps</c>: no architecture, no imports.
/// </summary>
internal sealed record ProtectedExecutable : ExecutableReading;
