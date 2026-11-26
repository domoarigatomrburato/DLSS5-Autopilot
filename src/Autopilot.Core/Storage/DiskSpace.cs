namespace Autopilot.Core.Storage;

/// <summary>Recognises a full drive however the error was wrapped.</summary>
public static class DiskSpace
{
    private const int ErrorDiskFull = unchecked((int)0x80070070);
    private const int ErrorHandleDiskFull = unchecked((int)0x80070027);

    // On Unix, .NET puts the raw errno in HResult (Interop.IOErrors.cs, GetIOException).
    private const int NoSpaceErrno = 28;

    /// <summary>Whether <paramref name="exception"/>, or anything it wraps, is a write that found the drive full.</summary>
    public static bool IsDiskFull(Exception exception) =>
        ExceptionChain.Of(exception).Any(e => e switch
        {
            IOException { HResult: ErrorDiskFull or ErrorHandleDiskFull } => true,
            IOException { HResult: NoSpaceErrno } => !OperatingSystem.IsWindows(),
            _ => false,
        });
}
