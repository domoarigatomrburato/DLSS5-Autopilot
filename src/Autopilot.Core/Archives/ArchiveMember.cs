namespace Autopilot.Core.Archives;

/// <summary>One file member of an archive: its path as the extractor's checks normalise it ('/' separated), and its declared size.</summary>
public sealed record ArchiveMember(string Path, long Size);
