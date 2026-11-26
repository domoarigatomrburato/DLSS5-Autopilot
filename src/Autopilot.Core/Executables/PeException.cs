using Autopilot.Core.Resources;

namespace Autopilot.Core.Executables;

/// <summary>
/// A file that is not a Windows executable this tool can read, with the reason. Unlike an
/// executable that simply imports nothing of interest, this is an error with a stated cause.
/// </summary>
public sealed class PeException : IOException
{
    // ARM64 and ARM64EC are named because a person can act on the name; other machines are a number.
    private const ushort Arm64 = 0xAA64;
    private const ushort Arm64Ec = 0xA641;

    internal PeException(PeFailure reason, string path, Exception? innerException = null, ushort? machine = null)
        : base(MessageFor(reason, path, innerException, machine), innerException)
    {
        Reason = reason;
        Path = path;
        Machine = machine;
    }

    public PeFailure Reason { get; }

    /// <summary>The file that was being read.</summary>
    public string Path { get; }

    /// <summary>The COFF machine type, for <see cref="PeFailure.UnsupportedMachine"/>.</summary>
    public ushort? Machine { get; }

    private static string MessageFor(PeFailure reason, string path, Exception? innerException, ushort? machine)
    {
        string name = System.IO.Path.GetFileName(path);
        return reason switch
        {
            PeFailure.NotExecutable => Texts.Format("PeNotExecutable", name),
            PeFailure.NoPeHeader => Texts.Format("PeNoPeHeader", name),
            PeFailure.UnsupportedMachine => Texts.Format("PeUnsupportedMachine", name, MachineName(machine.GetValueOrDefault())),
            PeFailure.CorruptHeader => Texts.Format("PeCorruptHeader", name),
            _ => Texts.Format("PeUnreadable", name, innerException?.Message),
        };
    }

    private static string MachineName(ushort machine) => machine switch
    {
        Arm64 => "ARM64",
        Arm64Ec => "ARM64EC",
        _ => $"0x{machine:X4}",
    };
}
