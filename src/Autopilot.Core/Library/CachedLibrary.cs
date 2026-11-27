namespace Autopilot.Core.Library;

/// <summary>The library as it was saved, with the games that changed since read again.</summary>
/// <param name="Games">One entry per game, in the order the library is shown in.</param>
/// <param name="ReadAgain">The entries of <paramref name="Games"/> that were read again because they changed or are protected.</param>
/// <param name="Notes">What reading those games again found that the log should say.</param>
public sealed record CachedLibrary(IReadOnlyList<GameEntry> Games, IReadOnlyList<GameEntry> ReadAgain, IReadOnlyList<LibraryNote> Notes);
