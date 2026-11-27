using Autopilot.Core.Executables;
using Autopilot.Core.Net;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Components;

/// <summary>Why a raw NVIDIA DLL was refused.</summary>
internal enum RawDllProblem
{
    /// <summary>Under 200,000 bytes: an error page or a stub, not a runtime.</summary>
    TooSmall,

    /// <summary>Not a Windows executable for x86 or x64: no <c>MZ</c>, no <c>PE\0\0</c>, another machine, or unreadable.</summary>
    NotExecutable,

    /// <summary>A 32-bit DLL: NVIDIA's runtimes are x64 (machine <c>0x8664</c>).</summary>
    Not64Bit,
}

/// <summary>A raw NVIDIA DLL that failed its checks, and why in words.</summary>
internal sealed record RawDllRejection(RawDllProblem Problem, string Message);

/// <summary>
/// What proves a file is NVIDIA's: the Authenticode signer every NVIDIA runtime carries (the mirror's
/// <c>nvngx_dlssnr.dll</c> included, since the mirror republishes NVIDIA's file) and, for a DLL taken
/// raw from NVIDIA's SDK tree, the PE checks of the integrity table.
/// </summary>
internal static class NvidiaFiles
{
    /// <summary>The signer NVIDIA's runtimes carry. The one place the name is written.</summary>
    public const string Publisher = "NVIDIA Corporation";

    /// <summary>The smallest a real runtime is; anything smaller is a page or a stub.</summary>
    public const long MinRawSize = 200_000;

    /// <summary>The signer check, as one of net's verification kinds.</summary>
    public static SignerVerification Signer { get; } = new(Publisher);

    /// <summary>
    /// The raw-DLL checks: 200,000 bytes or more, <c>MZ</c>, a <c>PE\0\0</c> signature at the header's
    /// offset, machine <c>0x8664</c>. <see langword="null"/> when the file passes.
    /// </summary>
    public static async Task<RawDllRejection?> CheckRawAsync(string path, CancellationToken cancellationToken)
    {
        string name = Path.GetFileName(path);
        long size = new FileInfo(path).Length;
        if (size < MinRawSize)
        {
            return new(RawDllProblem.TooSmall, Texts.Format("RawDllTooSmall", name, size));
        }

        try
        {
            using PeImage image = await PeImage.OpenAsync(path, cancellationToken);
            return image.Bitness == Bitness.Bits64 ? null : new(RawDllProblem.Not64Bit, Texts.Format("RawDllNot64Bit", name));
        }
        catch (PeException failure)
        {
            return new(RawDllProblem.NotExecutable, failure.Message);
        }
        catch (UnauthorizedAccessException failure)
        {
            return new(RawDllProblem.NotExecutable, new PeException(PeFailure.Unreadable, path, failure).Message);
        }
    }
}
