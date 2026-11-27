using Autopilot.Core.Graphics;
using Autopilot.Core.Install;

namespace Autopilot.Core.Library;

/// <summary>What reading a game needs from the host.</summary>
/// <param name="Choices">The person's choices per game folder.</param>
/// <param name="Records">Install's reader of its records.</param>
/// <param name="ApiSearch">Where the graphics API decision may look beyond the game's folder.</param>
public sealed record GameReading(IGameChoices Choices, IInstallRecords Records, ApiSearch ApiSearch);
