namespace Autopilot.Core.Library;

/// <summary>One game as read, and what the log should say about reading it.</summary>
internal sealed record GameRead(GameEntry Entry, IReadOnlyList<LibraryNote> Notes);
