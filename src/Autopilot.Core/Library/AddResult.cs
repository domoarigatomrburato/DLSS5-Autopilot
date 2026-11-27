using Autopilot.Core.Resources;

namespace Autopilot.Core.Library;

/// <summary>What adding a game by hand did.</summary>
public abstract record AddResult
{
    private protected AddResult()
    {
    }
}

/// <summary>The game was read and belongs in the library, with source <c>Manual</c>.</summary>
public sealed record GameAdded(GameEntry Entry, IReadOnlyList<LibraryNote> Notes) : AddResult;

/// <summary>The library already lists the game, by folder or by executable: that entry is the one to open.</summary>
public sealed record AlreadyListed(GameEntry Existing) : AddResult
{
    public string Explain() => Texts.Format("AddAlreadyListed", Existing.Name);
}

/// <summary>The folder holds no executable; nothing is added.</summary>
public sealed record NoExecutableToAdd(string Folder) : AddResult
{
    public string Explain() => Texts.Format("AddNoExecutable", Folder);
}

/// <summary>The path is neither an executable nor a folder.</summary>
public sealed record AddPathNotFound(string Path) : AddResult
{
    public string Explain() => Texts.Format("AddPathNotFound", Path);
}
