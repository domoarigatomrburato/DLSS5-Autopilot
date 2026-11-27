using Autopilot.Core.Library.Stores;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Library;

/// <summary>Something reading the library found that the log should say; the caller logs it.</summary>
public abstract record LibraryNote
{
    private protected LibraryNote()
    {
    }

    /// <summary>The note in words.</summary>
    public abstract string Explain();
}

/// <summary>A source failed as a whole; the others were read.</summary>
public sealed record SourceFailed(GameSource Source, Exception Error) : LibraryNote
{
    public override string Explain() => Texts.Format("NoteSourceFailed", Sources.Label(Source), Error.GetType().Name);
}

/// <summary>A game folder holds no executable; the entry is kept, with the reason.</summary>
public sealed record NoExecutableFound(string Folder, string Name) : LibraryNote
{
    public override string Explain() => Texts.Format("NoteNoExecutable", Folder, Name);
}

/// <summary>The search for executables stopped at its cap, so the folder may hold more.</summary>
public sealed record SearchStoppedEarly(string Folder, int FoldersSearched) : LibraryNote
{
    public override string Explain() => Texts.Format("NoteSearchStoppedEarly", Folder, FoldersSearched);
}

/// <summary>Reading one game took a second or more.</summary>
public sealed record SlowRead(string Name, TimeSpan Duration) : LibraryNote
{
    public override string Explain() => Texts.Format("NoteSlowRead", Name, Duration.TotalSeconds);
}

/// <summary>The store names a stub; the ranking found the game under a <c>Binaries</c> folder.</summary>
/// <param name="Name">The game.</param>
/// <param name="StoreExecutable">The file name the store names.</param>
/// <param name="Executable">The executable used instead, below the game folder.</param>
public sealed record StorePickReplaced(string Name, string StoreExecutable, string Executable) : LibraryNote
{
    public override string Explain() => Texts.Format("NoteStorePickReplaced", Name, StoreExecutable, Executable);
}

/// <summary>An earlier install is recorded in another folder than the executable's.</summary>
/// <param name="Name">The game.</param>
/// <param name="RecordFolder">Where the record is.</param>
/// <param name="Executable">The file name of the executable the game was listed against.</param>
public sealed record InstallRecordedElsewhere(string Name, string RecordFolder, string Executable) : LibraryNote
{
    public override string Explain() => Texts.Format("NoteInstallRecordedElsewhere", Name, RecordFolder, Executable);
}
