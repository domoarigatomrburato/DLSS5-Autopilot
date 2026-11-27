namespace Autopilot.Core.Library;

/// <summary>Why a game's executable could not be read.</summary>
public enum GameReadFailure
{
    /// <summary>The folder holds no executable.</summary>
    NoExecutable,

    /// <summary>The executable is not one this tool can read.</summary>
    Unreadable,
}

/// <summary>Why a game's executable could not be read, in words.</summary>
public sealed record GameReadError(GameReadFailure Failure, string Explanation);
