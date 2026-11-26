using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Autopilot.Core.Platform;
using Org.BouncyCastle.Crypto.Digests;

namespace Autopilot.Core.Library.Stores;

/// <summary>
/// The game folders the EA app's install file lists. The file, <c>%PROGRAMDATA%\EA Desktop\&lt;id&gt;\IS</c>,
/// is AES-256-CBC encrypted with a key derived from the machine's hardware; after 64 bytes the reader
/// skips comes JSON whose <c>installInfos</c> hold every owned game, the installed ones with a
/// <c>baseInstallPath</c>. Nothing when the file is missing, oversize, for another machine or malformed.
/// </summary>
internal static class EaDesktopInstalls
{
    private const string AllUsers = "allUsersGenericId";

    private const string InstallFile = "IS";

    // A hash of the file the reader does not need.
    private const int HeaderBytes = 64;

    private const int IvBytes = 16;

    public static async Task<IReadOnlyList<string>> FoldersAsync(string programData, HardwareIdentity identity, CancellationToken cancellationToken)
    {
        string path = Path.Join(programData, "EA Desktop", Convert.ToHexStringLower(Sha3(AllUsers)), InstallFile);
        byte[]? file = await StoreFiles.ReadAsync(path, cancellationToken);
        if (file is null || Decrypt(file, identity) is not { } plain)
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(plain);
            return InstalledFolders(document.RootElement);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static byte[]? Decrypt(byte[] file, HardwareIdentity identity)
    {
        if (file.Length <= HeaderBytes)
        {
            return null;
        }

        try
        {
            using var aes = Aes.Create();
            aes.Key = Key(identity);
            return aes.DecryptCbc(file.AsSpan(HeaderBytes), Sha3(AllUsers + InstallFile).AsSpan(0, IvBytes), PaddingMode.PKCS7);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    // SHA3-256 of "allUsersGenericIdIS" and the lowercase hex SHA-1 of every hardware value followed by ';'.
    private static byte[] Key(HardwareIdentity identity)
    {
        string hardware = string.Concat(
            new[]
            {
                identity.BaseBoardManufacturer, identity.BaseBoardSerial, identity.BiosManufacturer, identity.BiosSerial, identity.SystemVolumeSerial,
                identity.VideoControllerDeviceId, identity.ProcessorManufacturer, identity.ProcessorId, identity.ProcessorName,
            }.Select(value => value + ';'));
        return Sha3(AllUsers + InstallFile + Sha1Hex(hardware));
    }

    [SuppressMessage("Security", "CA5350", Justification = "EA's install file format derives its key from a SHA-1 hex digest; no security relies on it")]
    private static string Sha1Hex(string text) => Convert.ToHexStringLower(SHA1.HashData(Encoding.ASCII.GetBytes(text)));

    // .NET's own SHA3_256 is missing on Windows 10, so the digest comes from BouncyCastle.
    private static byte[] Sha3(string text)
    {
        Sha3Digest digest = new(256);
        byte[] input = Encoding.ASCII.GetBytes(text);
        digest.BlockUpdate(input);
        byte[] hash = new byte[digest.GetDigestSize()];
        _ = digest.DoFinal(hash);
        return hash;
    }

    // An owned game that is not installed has an empty baseInstallPath; an entry without its id or slug is skipped.
    private static List<string> InstalledFolders(JsonElement root)
    {
        List<string> folders = [];
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("installInfos", out JsonElement infos) || infos.ValueKind != JsonValueKind.Array)
        {
            return folders;
        }

        foreach (JsonElement info in infos.EnumerateArray())
        {
            if (!string.IsNullOrEmpty(StoreFiles.Text(info, "softwareId"))
                && !string.IsNullOrEmpty(StoreFiles.Text(info, "baseSlug"))
                && StoreFiles.Text(info, "baseInstallPath") is { } folder
                && DrivePaths.IsDirectory(folder))
            {
                folders.Add(folder);
            }
        }

        return folders;
    }
}
