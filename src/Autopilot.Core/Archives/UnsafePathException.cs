namespace Autopilot.Core.Archives;

/// <summary>A name that Windows would read as outside its folder, as a device or as a stream.</summary>
public sealed class UnsafePathException : Exception
{
    public UnsafePathException()
    {
    }

    public UnsafePathException(string message)
        : base(message)
    {
    }

    public UnsafePathException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    internal UnsafePathException(string name, string reason)
        : base($"'{name}' {reason}") => Name = name;

    /// <summary>The name as it was given.</summary>
    public string Name { get; } = "";
}
