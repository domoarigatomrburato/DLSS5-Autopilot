namespace Autopilot.Core.Net;

/// <summary>The exact host names a request may reach, compared ignoring case. No wildcards.</summary>
public sealed class HostAllowlist
{
    private readonly HashSet<string> _hosts = [with(StringComparer.OrdinalIgnoreCase)];

    public HostAllowlist(params IEnumerable<string> hosts)
    {
        foreach (string host in hosts)
        {
            if (string.IsNullOrWhiteSpace(host) || host.Contains('*', StringComparison.Ordinal))
            {
                throw new ArgumentException($"'{host}' is not an exact host name", nameof(hosts));
            }

            _ = _hosts.Add(host);
        }
    }

    public bool Allows(string host) => _hosts.Contains(host);
}
