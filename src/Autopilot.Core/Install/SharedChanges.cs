using System.Security;
using Autopilot.Core.Platform;
using Autopilot.Core.Resources;
using Autopilot.Core.Storage;

namespace Autopilot.Core.Install;

/// <summary>A value of the current user's registry set or deleted, made by the same executor as the files so the write steps see it first.</summary>
/// <param name="Registry">The current user's registry.</param>
/// <param name="Key">The key, below <c>HKEY_CURRENT_USER</c>.</param>
/// <param name="Name">The value's name.</param>
internal abstract record RegistryChange(IUserRegistry Registry, string Key, string Name) : DiskChange
{
    /// <summary>The value as the write steps and the failure note name it: <c>HKCU\&lt;key&gt;\&lt;name&gt;</c>.</summary>
    public string Place => $@"HKCU\{Key}\{Name}";

    /// <summary>What the write steps call it.</summary>
    public abstract WriteKind Kind { get; }

    /// <summary>Whether <paramref name="failure"/> is Windows refusing a registry read or write.</summary>
    public static bool IsRefusal(Exception failure) => failure is IOException or UnauthorizedAccessException or SecurityException;

    public override IEnumerable<WriteStep> Steps(Func<string, bool> isReadOnly) => [new(Kind, Place)];

    /// <summary>Makes the change.</summary>
    /// <exception cref="RegistryChangeException">Windows refused it.</exception>
    public Task ApplyAsync()
    {
        try
        {
            Apply(Registry);
        }
        catch (Exception refused) when (IsRefusal(refused))
        {
            throw RegistryChangeException.Write(Place, refused);
        }

        return Task.CompletedTask;
    }

    /// <summary>Makes the change in <paramref name="user"/>.</summary>
    protected abstract void Apply(IUserRegistry user);
}

/// <summary>A DWORD value set, the key created when missing.</summary>
/// <param name="Registry">The current user's registry.</param>
/// <param name="Key">The key.</param>
/// <param name="Name">The value's name.</param>
/// <param name="Value">The value.</param>
internal sealed record SetRegistryDword(IUserRegistry Registry, string Key, string Name, int Value) : RegistryChange(Registry, Key, Name)
{
    public override WriteKind Kind => WriteKind.SetRegistryValue;

    protected override void Apply(IUserRegistry user) => user.SetDword(Key, Name, Value);
}

/// <summary>A string value set, the key created when missing.</summary>
/// <param name="Registry">The current user's registry.</param>
/// <param name="Key">The key.</param>
/// <param name="Name">The value's name.</param>
/// <param name="Value">The value.</param>
internal sealed record SetRegistryString(IUserRegistry Registry, string Key, string Name, string Value) : RegistryChange(Registry, Key, Name)
{
    public override WriteKind Kind => WriteKind.SetRegistryValue;

    protected override void Apply(IUserRegistry user) => user.SetString(Key, Name, Value);
}

/// <summary>A value deleted; one already gone is deleted.</summary>
/// <param name="Registry">The current user's registry.</param>
/// <param name="Key">The key.</param>
/// <param name="Name">The value's name.</param>
internal sealed record DeleteRegistryValue(IUserRegistry Registry, string Key, string Name) : RegistryChange(Registry, Key, Name)
{
    public override WriteKind Kind => WriteKind.DeleteRegistryValue;

    protected override void Apply(IUserRegistry user) => user.DeleteValue(Key, Name);
}

/// <summary>
/// A file of the layer folder written over whatever is there, through a temporary file checked against its facts:
/// shared by every install, so it is no record entry. A file another program holds is a <see cref="LayerFileHeldException"/>.
/// </summary>
/// <param name="Path">The file, full.</param>
/// <param name="Facts">Its hash and size.</param>
/// <param name="Source">Its bytes.</param>
internal sealed record WriteLayerFile(string Path, FileFacts Facts, FileSource Source) : DiskChange
{
    public override IEnumerable<WriteStep> Steps(Func<string, bool> isReadOnly) => [new(WriteKind.Replace, Path)];

    /// <summary>The failure of a write another program's hold stopped, said as a layer file in use.</summary>
    public Exception Held(FileHeldException held)
    {
        ArgumentNullException.ThrowIfNull(held);
        return new LayerFileHeldException(Path, held.InnerException!);
    }
}

/// <summary>A registry value the install could not change, or a key it could not read: Windows refused it.</summary>
internal sealed class RegistryChangeException : ExplainedException
{
    private readonly string _textKey;

    private RegistryChangeException(string place, Exception innerException, string message, string textKey)
        : base(message, innerException)
    {
        Place = place;
        _textKey = textKey;
    }

    /// <summary>The value or the key refused: <c>HKCU\&lt;key&gt;</c>, with <c>\&lt;name&gt;</c> for a value.</summary>
    public string Place { get; }

    public override FailureKind Kind => FailureKind.Unexpected;

    public override string Text => Texts.Format(_textKey, Place, InnerException!.Message);

    /// <summary>A change of <paramref name="place"/> Windows refused.</summary>
    public static RegistryChangeException Write(string place, Exception innerException) =>
        new(place, innerException, $"{place} could not be changed: {innerException.Message}", "InstallRegistryRefused");

    /// <summary>A read of the key <paramref name="place"/> Windows refused.</summary>
    public static RegistryChangeException Read(string place, Exception innerException) =>
        new(place, innerException, $"{place} could not be read: {innerException.Message}", "InstallRegistryUnreadable");
}

/// <summary>A file of the layer folder another program holds (a running Vulkan or OpenXR program has the DLL loaded): the install stops.</summary>
internal sealed class LayerFileHeldException(string path, Exception innerException)
    : ExplainedException($"{path} is held by another program: {innerException.Message}", innerException)
{
    /// <summary>The file.</summary>
    public string FilePath { get; } = path;

    public override FailureKind Kind => FailureKind.Locked;

    public override FailureCause? Cause => new(Path: FilePath);

    public override string Text => Texts.Format("InstallLayerHeld", Path.GetFileName(FilePath), ExceptionChain.Of(this).Last().Message);
}

/// <summary>A failure that says itself what stopped the install, for its note: its kind, its detail and its cause paragraph.</summary>
internal abstract class ExplainedException(string message, Exception innerException) : IOException(message, innerException)
{
    /// <summary>What stopped the install.</summary>
    public abstract FailureKind Kind { get; }

    /// <summary>The detail behind <see cref="Kind"/>; null when it has none.</summary>
    public virtual FailureCause? Cause => null;

    /// <summary>The cause paragraph, which follows the opening sentence.</summary>
    public abstract string Text { get; }
}
