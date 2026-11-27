using Autopilot.Core.Resources;

namespace Autopilot.Core.Graphics;

/// <summary>Graphics APIs as the person reads them.</summary>
internal static class ApiNames
{
    /// <summary>An API as the person reads it in a reason.</summary>
    public static string Of(GraphicsApi api) => api switch
    {
        GraphicsApi.Unknown => Texts.Text("GameApiUnknown"),
        GraphicsApi.Dxgi => Texts.Text("GameApiDxgi"),
        _ => api.ToString(),
    };
}
