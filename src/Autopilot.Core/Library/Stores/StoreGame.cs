namespace Autopilot.Core.Library.Stores;

/// <summary>A game a store or a folder search says is installed.</summary>
/// <param name="Name">What the store calls it, else the folder's name.</param>
/// <param name="Folder">The game's root folder, as the store spells it.</param>
/// <param name="Source">Which source found it.</param>
/// <param name="StoreExecutable">The executable the store itself launches, as a full path, only when that file exists.</param>
public sealed record StoreGame(string Name, string Folder, GameSource Source, string? StoreExecutable);
