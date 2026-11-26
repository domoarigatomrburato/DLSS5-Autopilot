using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Autopilot.Core.Platform;
using Org.BouncyCastle.Crypto.Digests;

namespace Autopilot.Core.Tests.Library.Stores;

/// <summary>
/// Writes the EA app's encrypted install file the way the app does, from the format's description:
/// 64 bytes the reader skips, then AES-256-CBC with PKCS#7 padding of the JSON. The key is SHA3-256 of
/// <c>"allUsersGenericIdIS"</c> followed by the lowercase hex SHA-1 of the machine's hardware string;
/// the IV is the first 16 bytes of SHA3-256 of <c>"allUsersGenericIdIS"</c>.
/// </summary>
internal static class EaDesktopFile
{
    /// <summary>Where the app keeps the file below <c>%PROGRAMDATA%</c>: the folder is the hex SHA3-256 of <c>"allUsersGenericId"</c>.</summary>
    public const string RelativePath = "ProgramData/EA Desktop/530c11479fe252fc5aabc24935b9776d4900eb3ba58fdc271e0d6229413ad40e/IS";

    /// <summary>A machine's hardware values, as the Windows side would read them.</summary>
    public static readonly HardwareIdentity Machine = new(
        "ASUSTeK COMPUTER INC.", "210686459200389", "American Megatrends Inc.", "System Serial Number", "5A3C1F0E",
        @"PCI\VEN_10DE&DEV_2684&SUBSYS_16F310DE&REV_A1\4&2F1B4F2A&0&0019", "AuthenticAMD", "178BFBFF00A20F12", "AMD Ryzen 9 7950X 16-Core Processor");

    /// <summary>The hardware string of <see cref="Machine"/>: every value followed by a semicolon, in the order the key uses them.</summary>
    public const string MachineString =
        @"ASUSTeK COMPUTER INC.;210686459200389;American Megatrends Inc.;System Serial Number;5A3C1F0E;PCI\VEN_10DE&DEV_2684&SUBSYS_16F310DE&REV_A1\4&2F1B4F2A&0&0019;AuthenticAMD;178BFBFF00A20F12;AMD Ryzen 9 7950X 16-Core Processor;";

    /// <summary>The whole file for <paramref name="json"/>, encrypted for the machine whose hardware string is <paramref name="hardware"/>.</summary>
    public static byte[] Encrypt(string json, string hardware = MachineString) => Encrypt(Encoding.UTF8.GetBytes(json), hardware);

    public static byte[] Encrypt(byte[] plain, string hardware = MachineString)
    {
        using var aes = Aes.Create();
        aes.Key = Key(hardware);
        byte[] cipher = aes.EncryptCbc(plain, Iv(), PaddingMode.PKCS7);
        return [.. new byte[64], .. cipher];
    }

    public static byte[] Key(string hardware) => Sha3("allUsersGenericIdIS" + Sha1Hex(hardware));

    public static byte[] Iv() => Sha3("allUsersGenericIdIS")[..16];

    public static byte[] Sha3(string text)
    {
        Sha3Digest digest = new(256);
        byte[] input = Encoding.ASCII.GetBytes(text);
        digest.BlockUpdate(input, 0, input.Length);
        byte[] hash = new byte[digest.GetDigestSize()];
        _ = digest.DoFinal(hash, 0);
        return hash;
    }

    [SuppressMessage("Security", "CA5350", Justification = "EA's install file format derives its key from a SHA-1 hex digest; no security relies on it")]
    public static string Sha1Hex(string text) => Convert.ToHexStringLower(SHA1.HashData(Encoding.ASCII.GetBytes(text)));
}
