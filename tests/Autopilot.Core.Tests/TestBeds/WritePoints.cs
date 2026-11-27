using Autopilot.Core.Storage;

namespace Autopilot.Core.Tests.TestBeds;

/// <summary>A crash the test made happen: the process "died" before this step.</summary>
internal sealed class SimulatedCrash(WriteStep step) : Exception($"simulated crash before {step.Kind} {step.Path}")
{
    public WriteStep Step { get; } = step;
}

/// <summary>
/// A crash after the first <c>n</c> steps: from step <c>n + 1</c> on, every step throws
/// <see cref="SimulatedCrash"/>, so a rollback tried after it fails too and the disk stays as the
/// crash left it, for a test to replay recovery from the journal.
/// </summary>
internal sealed class CrashPoints : IWriteSteps
{
    private readonly int _survives;
    private readonly List<WriteStep> _made = [];

    private CrashPoints(int survives) => _survives = survives;

    /// <summary>The steps that went ahead, in order.</summary>
    public IReadOnlyList<WriteStep> Made => _made;

    /// <summary>Lets <paramref name="steps"/> steps go ahead (0: none), then crashes at every later one.</summary>
    public static CrashPoints After(int steps) => new(steps);

    public void Before(WriteStep step)
    {
        if (_made.Count >= _survives)
        {
            throw new SimulatedCrash(step);
        }

        _made.Add(step);
    }
}

/// <summary>How a full drive surfaces.</summary>
internal enum DiskFullError
{
    /// <summary>Unix: <c>ENOSPC</c>, errno 28, as .NET puts it in <see cref="Exception.HResult"/>.</summary>
    Errno28,

    /// <summary>Windows: <c>ERROR_DISK_FULL</c> (112).</summary>
    Windows112,

    /// <summary>Windows: <c>ERROR_HANDLE_DISK_FULL</c> (39).</summary>
    Windows39,
}

/// <summary>
/// A full drive at step <c>n</c> (counted from 1): that step throws once an <see cref="IOException"/>
/// carrying the chosen error, optionally wrapped in another exception; every other step goes ahead.
/// </summary>
internal sealed class DiskFullPoints : IWriteSteps
{
    private readonly int _at;
    private readonly DiskFullError _error;
    private readonly bool _wrapped;
    private int _steps;

    private DiskFullPoints(int at, DiskFullError error, bool wrapped)
    {
        _at = at;
        _error = error;
        _wrapped = wrapped;
    }

    /// <summary>The drive fills at step <paramref name="step"/>, with <paramref name="error"/>, wrapped in an <see cref="InvalidOperationException"/> when <paramref name="wrapped"/>.</summary>
    public static DiskFullPoints At(int step, DiskFullError error = DiskFullError.Windows112, bool wrapped = false) => new(step, error, wrapped);

    /// <summary>The exception a full drive raises, as <see cref="At"/> throws it.</summary>
    public static Exception Failure(DiskFullError error, bool wrapped = false)
    {
        IOException full = new("There is not enough space on the disk.", Code(error));
        return wrapped ? new InvalidOperationException("the write failed", full) : full;
    }

    /// <summary>The <see cref="Exception.HResult"/> a full drive carries, as .NET reports <paramref name="error"/>.</summary>
    public static int Code(DiskFullError error) => error switch
    {
        DiskFullError.Errno28 => 28,
        DiskFullError.Windows39 => unchecked((int)0x80070027),
        _ => unchecked((int)0x80070070),
    };

    public void Before(WriteStep step)
    {
        if (++_steps == _at)
        {
            throw Failure(_error, _wrapped);
        }
    }
}
