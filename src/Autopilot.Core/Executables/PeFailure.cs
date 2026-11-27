namespace Autopilot.Core.Executables;

/// <summary>Why a file could not be read as a Windows executable.</summary>
internal enum PeFailure
{
    /// <summary>The file does not start with <c>MZ</c>, or is shorter than a DOS header.</summary>
    NotExecutable,

    /// <summary>The DOS header does not point at a <c>PE</c> signature.</summary>
    NoPeHeader,

    /// <summary>The executable is built for a processor other than x86 or x64.</summary>
    UnsupportedMachine,

    /// <summary>The headers contradict themselves or are cut short.</summary>
    CorruptHeader,

    /// <summary>The file could not be opened or read.</summary>
    Unreadable,
}
