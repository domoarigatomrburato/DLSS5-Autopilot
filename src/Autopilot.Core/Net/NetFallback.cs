namespace Autopilot.Core.Net;

/// <summary>A source another one stands in for: its network failure is no answer, while a refusal still propagates.</summary>
internal static class NetFallback
{
    /// <summary>
    /// The answer of <paramref name="pending"/>; <see langword="default"/> when the network failed,
    /// after <paramref name="onFailure"/> has seen the failure. A refusal is never covered.
    /// </summary>
    public static async Task<T?> OrNullAsync<T>(Task<T> pending, Action<NetException>? onFailure = null)
    {
        try
        {
            return await pending;
        }
        catch (NetException failure) when (!failure.IsRefusal)
        {
            onFailure?.Invoke(failure);
            return default;
        }
    }
}
