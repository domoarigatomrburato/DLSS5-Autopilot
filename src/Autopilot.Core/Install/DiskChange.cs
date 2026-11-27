using Autopilot.Core.Storage;

namespace Autopilot.Core.Install;

/// <summary>One change on disk an install makes, as data: <see cref="InstallDisk.RunAsync"/> makes it, <see cref="Steps"/> predicts it.</summary>
internal abstract record DiskChange
{
    /// <summary>The write steps the change is told as, a delete clearing read-only first when <paramref name="isReadOnly"/> says the file is.</summary>
    public abstract IEnumerable<WriteStep> Steps(Func<string, bool> isReadOnly);
}

/// <summary>A rename to a name that must be free.</summary>
/// <param name="From">The file.</param>
/// <param name="To">Its new name.</param>
internal sealed record MoveFile(string From, string To) : DiskChange
{
    public override IEnumerable<WriteStep> Steps(Func<string, bool> isReadOnly) => [new(WriteKind.Move, From, To)];
}

/// <summary>A game's file renamed to its backup name; a failure is a backup that could not be made.</summary>
/// <param name="From">The file.</param>
/// <param name="To">Its backup name.</param>
internal sealed record BackUpFile(string From, string To) : DiskChange
{
    public override IEnumerable<WriteStep> Steps(Func<string, bool> isReadOnly) => [new(WriteKind.Move, From, To)];
}

/// <summary>A file written where nothing is, checked against its facts before it takes its name.</summary>
/// <param name="Path">Where.</param>
/// <param name="Facts">Its hash and size.</param>
/// <param name="Source">Its bytes.</param>
internal sealed record WriteFile(string Path, FileFacts Facts, FileSource Source) : DiskChange
{
    public override IEnumerable<WriteStep> Steps(Func<string, bool> isReadOnly) => [new(WriteKind.Create, Path)];
}

/// <summary>A file deleted, its read-only attribute cleared first.</summary>
/// <param name="Path">The file.</param>
internal sealed record DeleteFile(string Path) : DiskChange
{
    public override IEnumerable<WriteStep> Steps(Func<string, bool> isReadOnly) =>
        isReadOnly(Path) ? [new(WriteKind.SetAttributes, Path), new(WriteKind.Delete, Path)] : [new(WriteKind.Delete, Path)];
}

/// <summary>A folder created.</summary>
/// <param name="Path">The folder.</param>
internal sealed record CreateFolderChange(string Path) : DiskChange
{
    public override IEnumerable<WriteStep> Steps(Func<string, bool> isReadOnly) => [new(WriteKind.CreateFolder, Path)];
}
