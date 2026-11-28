using System.Collections.Frozen;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Autopilot.Core.Net;
using Microsoft.Win32.SafeHandles;

namespace Autopilot.Windows.Signatures;

/// <summary>
/// Checks a file's Authenticode signature with <c>WinVerifyTrust</c>, the way Windows does before it
/// runs a program. Revocation is checked over the whole chain from Windows' URL cache only, so a check
/// never goes to the network; a timestamped signature stays valid after its certificate expires.
/// </summary>
public sealed class AuthenticodeVerifier : ISignatureVerifier
{
    private const int TrustNoSignature = unchecked((int)0x800B0100);
    private const int TrustSubjectFormUnknown = unchecked((int)0x800B0003);
    private const int TrustProviderUnknown = unchecked((int)0x800B0001);
    private const int TrustBadDigest = unchecked((int)0x80096010);
    private const int TrustExplicitDistrust = unchecked((int)0x800B0111);
    private const int CertUntrustedRoot = unchecked((int)0x800B0109);
    private const int CertChaining = unchecked((int)0x800B010A);
    private const int CertRevoked = unchecked((int)0x800B010C);
    private const int CertExpired = unchecked((int)0x800B0101);
    private const int CryptRevocationOffline = unchecked((int)0x80092013);
    private const int CryptNoRevocationCheck = unchecked((int)0x80092012);
    private const int CertRevocationFailure = unchecked((int)0x800B010E);

    // What WinVerifyTrust's answer means. Anything not listed is Invalid, with the HRESULT in the detail.
    private static readonly FrozenDictionary<int, SignatureStatus> _statuses = new Dictionary<int, SignatureStatus>
    {
        [0] = SignatureStatus.Valid,
        [TrustNoSignature] = SignatureStatus.NotSigned,
        [TrustSubjectFormUnknown] = SignatureStatus.NotSigned,
        [TrustProviderUnknown] = SignatureStatus.NotSigned,
        [TrustBadDigest] = SignatureStatus.Invalid,
        [CertUntrustedRoot] = SignatureStatus.Untrusted,
        [CertChaining] = SignatureStatus.Untrusted,
        [TrustExplicitDistrust] = SignatureStatus.Untrusted,
        [CertRevoked] = SignatureStatus.Untrusted,
        [CertExpired] = SignatureStatus.Expired,
    }.ToFrozenDictionary();

    // Revocation that cannot be determined. WinVerifyTrust answers one HRESULT and does not say which
    // error wins, so these could hide an untrusted chain: the file is verified again without
    // revocation, and only that answer decides. A known revocation (CERT_E_REVOKED) is not among them.
    private static readonly FrozenSet<int> _revocationUnknown = new[] { CryptRevocationOffline, CryptNoRevocationCheck, CertRevocationFailure }.ToFrozenSet();

    /// <inheritdoc/>
    public Task<SignatureCheck> CheckAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() => Check(path), cancellationToken);
    }

    private static SignatureCheck Check(string path)
    {
        try
        {
            (int result, SignatureCheck check) = Verify(path, checkRevocation: true);
            if (!_revocationUnknown.Contains(result))
            {
                return check;
            }
            (_, SignatureCheck again) = Verify(path, checkRevocation: false);
            return again.Status == SignatureStatus.Valid ? again with { Detail = "revocation was not checked: " + Describe(result) } : again;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new(SignatureStatus.Unreadable, null, e.Message);
        }
    }

    private static unsafe (int Result, SignatureCheck Check) Verify(string path, bool checkRevocation)
    {
        // Windows reads the file through a handle of its own for each call, shared for reading only, so
        // it cannot change while it is checked and every call starts reading from the beginning.
        using SafeFileHandle file = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        fixed (char* name = path)
        {
            WintrustFileInfo info = new()
            {
                Size = (uint)sizeof(WintrustFileInfo),
                FilePath = name,
                File = file.DangerousGetHandle(),
            };
            WintrustData data = new()
            {
                Size = (uint)sizeof(WintrustData),
                UiChoice = WintrustData.UiNone,
                RevocationChecks = checkRevocation ? WintrustData.RevokeWholeChain : WintrustData.RevokeNone,
                UnionChoice = WintrustData.ChoiceFile,
                File = &info,
                StateAction = WintrustData.StateActionVerify,
                ProviderFlags = WintrustData.CacheOnlyUrlRetrieval | (checkRevocation ? 0 : WintrustData.RevocationCheckNone),
            };
            Guid action = NativeMethods.GenericVerifyV2;
            try
            {
                int result = NativeMethods.WinVerifyTrust(0, &action, &data);
                int lastError = Marshal.GetLastPInvokeError();
                return (result, Interpret(result, lastError, data.StateData));
            }
            finally
            {
                data.StateAction = WintrustData.StateActionClose;
                _ = NativeMethods.WinVerifyTrust(0, &action, &data);
            }
        }
    }

    private static SignatureCheck Interpret(int result, int lastError, nint stateData)
    {
        SignatureStatus status = _statuses.GetValueOrDefault(result, SignatureStatus.Invalid);
        string? detail = result == 0 ? null : Describe(result);
        // Microsoft's sample: TRUST_E_NOSIGNATURE means "no signature" only when the last error says so
        // too; otherwise a signature is there and could not be verified.
        if (result == TrustNoSignature && _statuses.GetValueOrDefault(lastError, SignatureStatus.Invalid) != SignatureStatus.NotSigned)
        {
            status = SignatureStatus.Invalid;
            detail += "; " + Describe(lastError);
        }
        string? signer = SignerOf(stateData);
        return status == SignatureStatus.Valid && signer is null
            ? new(SignatureStatus.Unreadable, null, "Windows trusts the signature, but its signer could not be read")
            : new(status, signer, detail);
    }

    // The signing (leaf) certificate's subject simple name, read from the verification's state.
    private static unsafe string? SignerOf(nint stateData)
    {
        nint provider = stateData == 0 ? 0 : NativeMethods.WTHelperProvDataFromStateData(stateData);
        ProviderSigner* signer = provider == 0 ? null : NativeMethods.WTHelperGetProvSignerFromChain(provider, 0, false, 0);
        if (signer == null || signer->CertificateCount == 0 || signer->Certificates == null || signer->Certificates->Certificate == 0)
        {
            return null;
        }
        try
        {
            // The certificate copies the context, so it outlives the state closed after the check.
            using X509Certificate2 certificate = new(signer->Certificates->Certificate);
            return certificate.GetNameInfo(X509NameType.SimpleName, false);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static string Describe(int code) =>
        string.Create(CultureInfo.InvariantCulture, $"0x{code:X8}: {Marshal.GetPInvokeErrorMessage(code).TrimEnd()}");
}
