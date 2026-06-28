using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace OrionLauncher.Services;

public enum Reachability
{
    Down,       // nothing answered — host unreachable or filtered
    HostUp,     // host responded (TCP RST or ICMP) but game port isn't TCP-open
    PortOpen    // a TCP service accepted a connection on the port
}

/// <summary>
/// Best-effort "is the server box reachable" probe. The Isle's game port is UDP
/// and EOS-relayed, so there's no reliable TCP service to confirm the game server
/// itself — this checks host/port reachability and reports it honestly.
/// Never throws.
/// </summary>
public class ReachabilityService
{
    private const int DefaultPort = 7777;

    public async Task<Reachability> CheckAsync(string? ipPort, CancellationToken ct = default)
    {
        var (host, port) = Parse(ipPort);
        if (host is null) return Reachability.Down;

        var tcp = await TryTcpAsync(host, port, 2500, ct);
        if (tcp == TcpResult.Open)    return Reachability.PortOpen;
        if (tcp == TcpResult.Refused) return Reachability.HostUp; // host answered with RST

        return await TryPingAsync(host, 2000) ? Reachability.HostUp : Reachability.Down;
    }

    private static (string? host, int port) Parse(string? ipPort)
    {
        if (string.IsNullOrWhiteSpace(ipPort)) return (null, 0);
        var s = ipPort.Trim();
        var i = s.LastIndexOf(':');
        if (i > 0 && int.TryParse(s[(i + 1)..], out var p))
            return (s[..i], p);
        return (s, DefaultPort);
    }

    private enum TcpResult { Open, Refused, Timeout }

    private static async Task<TcpResult> TryTcpAsync(string host, int port, int timeoutMs, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            using var cts    = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            await client.ConnectAsync(host, port, cts.Token);
            return TcpResult.Open;
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
        {
            return TcpResult.Refused;
        }
        catch
        {
            return TcpResult.Timeout;
        }
    }

    private static async Task<bool> TryPingAsync(string host, int timeoutMs)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, timeoutMs);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }
}
