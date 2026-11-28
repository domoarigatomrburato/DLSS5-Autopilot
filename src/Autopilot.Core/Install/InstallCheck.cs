namespace Autopilot.Core.Install;

/// <summary>What stops an install before anything is written: one per row of install.md's "Refusal checks" and routes.md's pre-install checks.</summary>
public enum BlockerKind
{
    /// <summary>The game cannot be set up as read: no executable, a read error, a protected executable without its architecture or API, no architecture, D3D8 on 64-bit.</summary>
    Game,

    /// <summary>The graphics API is not settled (Direct3D 11 or 12 unknown, or nothing read).</summary>
    Api,

    /// <summary>The install folder is not there.</summary>
    FolderMissing,

    /// <summary>The current user may not write into the install folder.</summary>
    FolderDenied,

    /// <summary>The install folder is an Xbox / Game Pass one, which only the Xbox app may write to.</summary>
    StoreFolder,

    /// <summary>A program from the game's folders is running.</summary>
    GameRunning,

    /// <summary>A file the install changes is held by another program.</summary>
    Locked,

    /// <summary>The folder's install record cannot be read, or belongs to another game folder.</summary>
    Record,

    /// <summary>The install would reach outside the game folder, or names a path twice.</summary>
    Plan,

    /// <summary>A file the install must read first cannot be read.</summary>
    Unreadable,

    /// <summary>The game's drive has not the room the install needs.</summary>
    FreeSpace,

    /// <summary>A Remix game on a route that is not remix.</summary>
    RemixOnly,

    /// <summary>The remix route and no Remix runtime.</summary>
    NoRemixRuntime,

    /// <summary>The remix route, a runtime without the neural pass, and no swap.</summary>
    RemixWithoutNeuralPass,

    /// <summary>A card DLSS cannot run on, not overridden.</summary>
    Card,

    /// <summary>The OptiScaler build is for another card.</summary>
    OptiScalerBuild,

    /// <summary>The person's own frame generation files cannot be placed.</summary>
    OwnFrameGeneration,

    /// <summary>The motion-vector provider is one the feeder cannot read.</summary>
    MotionProvider,

    /// <summary>The person's own <c>renodx-dlss5</c> add-on is missing or not 64-bit.</summary>
    LocalAddOn,

    /// <summary>The feeder build is older than the game or the settings need.</summary>
    FeederBuild,

    /// <summary>GITC-Uplift on Vulkan or OpenGL with a ReShade older than it needs.</summary>
    UpliftReShade,

    /// <summary>The Remix runtime's <c>d3d9.dll</c> is missing or cannot be opened.</summary>
    RemixRuntimeUnreadable,

    /// <summary>The name ReShade loads as is taken by something that is not ReShade (DXVK, REFramework, Special K, another injector).</summary>
    ProxyTaken,
}

/// <summary>One reason an install cannot go ahead, ending with what to do.</summary>
/// <param name="Kind">Which check refused.</param>
/// <param name="Text">The reason in words, ending with what to do.</param>
public sealed record InstallBlocker(BlockerKind Kind, string Text);

/// <summary>
/// The first phase of an install, read-only: every blocker found (not only the first) and the warnings.
/// The install goes ahead only without blockers.
/// </summary>
/// <param name="Blockers">Every reason the install cannot go ahead, in the order checked.</param>
/// <param name="Warnings">What the person should know before the install, which does not stop it.</param>
public sealed record InstallCheck(IReadOnlyList<InstallBlocker> Blockers, IReadOnlyList<string> Warnings)
{
    /// <summary>Whether nothing stops the install.</summary>
    public bool CanInstall => Blockers.Count == 0;

    /// <summary>The plan made concrete against the folder, when its record and paths allowed it: what the preview shows and the transaction runs.</summary>
    internal ResolvedInstall? Resolved { get; init; }
}
