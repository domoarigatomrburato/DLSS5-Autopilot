namespace Autopilot.Core.Graphics;

/// <summary>Where the host lets the decision look beyond the game's folder.</summary>
/// <param name="DocumentsFolders">
/// The person's Documents folders, in order: the shell's Documents known folder, then its OneDrive
/// variant. Unreal Engine 3 keeps the player's settings under <c>My Games</c> there.
/// </param>
public sealed record ApiSearch(IReadOnlyList<string> DocumentsFolders);
