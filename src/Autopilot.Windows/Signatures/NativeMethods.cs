using System.Runtime.InteropServices;

namespace Autopilot.Windows.Signatures;

/// <summary>The parts of <c>wintrust.dll</c> the Authenticode check calls, with source-generated marshalling.</summary>
internal static unsafe partial class NativeMethods
{
    private const string WinTrust = "wintrust.dll";

    /// <summary><c>WINTRUST_ACTION_GENERIC_VERIFY_V2</c>: Authenticode's policy provider.</summary>
    public static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");

    [LibraryImport(WinTrust, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static partial int WinVerifyTrust(nint window, Guid* action, WintrustData* data);

    /// <summary>The provider data (<c>CRYPT_PROVIDER_DATA</c>) behind a verification's state handle.</summary>
    [LibraryImport(WinTrust)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static partial nint WTHelperProvDataFromStateData(nint stateData);

    [LibraryImport(WinTrust)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static partial ProviderSigner* WTHelperGetProvSignerFromChain(
        nint providerData,
        uint signerIndex,
        [MarshalAs(UnmanagedType.Bool)] bool counterSigner,
        uint counterSignerIndex);
}

/// <summary><c>WINTRUST_FILE_INFO</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct WintrustFileInfo
{
    public uint Size;
    public char* FilePath;
    public nint File;
    public Guid* KnownSubject;
}

/// <summary><c>WINTRUST_DATA</c>, with the file member of its union.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct WintrustData
{
    public const uint UiNone = 2;
    public const uint RevokeNone = 0;
    public const uint RevokeWholeChain = 1;
    public const uint ChoiceFile = 1;
    public const uint StateActionVerify = 1;
    public const uint StateActionClose = 2;
    public const uint RevocationCheckNone = 0x10;
    public const uint CacheOnlyUrlRetrieval = 0x1000;

    public uint Size;
    public nint PolicyCallbackData;
    public nint SipClientData;
    public uint UiChoice;
    public uint RevocationChecks;
    public uint UnionChoice;
    public WintrustFileInfo* File;
    public uint StateAction;
    public nint StateData;
    public char* UrlReference;
    public uint ProviderFlags;
    public uint UiContext;
    public nint SignatureSettings;
}

/// <summary>The leading members of <c>CRYPT_PROVIDER_SGNR</c>, up to the certificate chain.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ProviderSigner
{
    public uint Size;
    public uint VerifyAsOfLow;
    public uint VerifyAsOfHigh;
    public uint CertificateCount;
    public ProviderCertificate* Certificates;
}

/// <summary>The leading members of <c>CRYPT_PROVIDER_CERT</c>, up to the certificate context.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ProviderCertificate
{
    public uint Size;
    public nint Certificate;
}
