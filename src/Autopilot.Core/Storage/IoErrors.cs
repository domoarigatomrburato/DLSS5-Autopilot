namespace Autopilot.Core.Storage;

/// <summary>
/// The one home of the I/O error codes the tool acts on: a full drive, and a file another program
/// holds. Each is recognised however the error was wrapped.
/// </summary>
public static class IoErrors
{
    private const int ErrorDiskFull = unchecked((int)0x80070070);
    private const int ErrorHandleDiskFull = unchecked((int)0x80070027);

    // On Unix, .NET puts the raw errno in HResult (Interop.IOErrors.cs, GetIOException).
    private const int NoSpaceErrno = 28;

    // Windows: ERROR_SHARING_VIOLATION and ERROR_LOCK_VIOLATION as HRESULTs, which no errno can be.
    private const int SharingViolation = unchecked((int)0x80070020);
    private const int LockViolation = unchecked((int)0x80070021);

    // Unix: .NET's FileShare is an flock, and a held one surfaces as EWOULDBLOCK with the raw errno
    // in HResult: 11 on Linux, 35 on macOS and BSD.
    private static readonly int _wouldBlockErrno = OperatingSystem.IsLinux() || OperatingSystem.IsAndroid() ? 11 : 35;

    /// <summary>Whether <paramref name="exception"/>, or anything it wraps, is a write that found the drive full.</summary>
    public static bool IsDiskFull(Exception exception) =>
        ExceptionChain.Of(exception).Any(e => e switch
        {
            IOException { HResult: ErrorDiskFull or ErrorHandleDiskFull } => true,
            IOException { HResult: NoSpaceErrno } => !OperatingSystem.IsWindows(),
            _ => false,
        });

    /// <summary>Whether <paramref name="exception"/>, or anything it wraps, is an open or a replace that failed because another handle holds the file.</summary>
    public static bool IsSharingViolation(Exception exception) =>
        ExceptionChain.Of(exception).Any(e => e switch
        {
            IOException { HResult: SharingViolation or LockViolation } => true,
            IOException io when io.HResult == _wouldBlockErrno => !OperatingSystem.IsWindows(),
            _ => false,
        });
}
