using Autopilot.Core.Library.Stores;
using Autopilot.Core.Resources;

namespace Autopilot.Core.Library;

/// <summary>What a person reads for a source.</summary>
internal static class Sources
{
    public static string Label(GameSource source) => Texts.Text($"Source{source}");
}
