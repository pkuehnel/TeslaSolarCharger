using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace TeslaSolarCharger.Server.Services.Https;

public enum HttpsUrlOutcome
{
    Added,
    AlreadyConfigured,
    Disabled,
    NoUrlsConfigured,
    PortUnavailable,
}

public record HttpsUrlDecision(HttpsUrlOutcome Outcome, string? Urls, int Port);

/// <summary>
/// Decides whether TSC adds an HTTPS endpoint to the URLs it listens on (ASPNETCORE_URLS), before the web host starts.
/// </summary>
public static partial class HttpsUrlConfigurator
{
    /// <param name="configuredUrls">The configured URLs, separated by semicolons.</param>
    /// <param name="httpsPort">The port to add HTTPS on, 0 or less disables it.</param>
    /// <param name="isPortAvailable">Whether nothing else listens on a port yet.</param>
    public static HttpsUrlDecision Decide(string? configuredUrls, int httpsPort, Func<int, bool> isPortAvailable)
    {
        if (httpsPort <= 0)
        {
            return new(HttpsUrlOutcome.Disabled, configuredUrls, httpsPort);
        }
        if (string.IsNullOrWhiteSpace(configuredUrls))
        {
            return new(HttpsUrlOutcome.NoUrlsConfigured, configuredUrls, httpsPort);
        }
        var urls = configuredUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (urls.Any(url => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            return new(HttpsUrlOutcome.AlreadyConfigured, configuredUrls, httpsPort);
        }
        //The configured URLs are not bound yet, so a port they use still looks available
        if (urls.Any(url => GetPort(url) == httpsPort) || !isPortAvailable(httpsPort))
        {
            return new(HttpsUrlOutcome.PortUnavailable, configuredUrls, httpsPort);
        }
        return new(HttpsUrlOutcome.Added, string.Join(';', urls.Append($"https://+:{httpsPort}")), httpsPort);
    }

    /// <summary>
    /// The decision is made before logging is set up, so it is logged afterwards.
    /// </summary>
    public static void LogDecision(ILogger logger, HttpsUrlDecision decision)
    {
        switch (decision.Outcome)
        {
            case HttpsUrlOutcome.Added:
                logger.LogInformation("Listening for HTTPS on port {port}", decision.Port);
                break;
            case HttpsUrlOutcome.AlreadyConfigured:
                logger.LogInformation("HTTPS is configured in ASPNETCORE_URLS, it uses TSC's certificates");
                break;
            case HttpsUrlOutcome.Disabled:
                logger.LogInformation("HTTPS is disabled, as HttpsPort is {port}", decision.Port);
                break;
            case HttpsUrlOutcome.NoUrlsConfigured:
                logger.LogInformation("No URLs are configured in ASPNETCORE_URLS, so no HTTPS endpoint is added");
                break;
            case HttpsUrlOutcome.PortUnavailable:
                logger.LogWarning("HTTPS port {port} is already in use, so TSC is only available via HTTP. Set the environment variable HttpsPort to a free port.", decision.Port);
                break;
        }
    }

    /// <summary>
    /// Kestrel listens on all IPv6 and IPv4 addresses for https://+:port. Windows lets such a listener share the port
    /// with a program listening on IPv4 only, but that program would still get the IPv4 connections, so both are
    /// checked.
    /// </summary>
    public static bool IsTcpPortAvailable(int port) => IsTcpPortAvailable(port, IPAddress.IPv6Any, IPAddress.Any);

    /// <summary>
    /// Tests use loopback addresses, as listening on all addresses makes Windows ask to allow it in the firewall.
    /// </summary>
    public static bool IsTcpPortAvailable(int port, IPAddress ipv6Address, IPAddress ipv4Address) =>
        CanListenOnIpv6(ipv6Address, port) && CanListen(ipv4Address, port, false);

    private static bool CanListenOnIpv6(IPAddress ipv6Address, int port)
    {
        try
        {
            return CanListen(ipv6Address, port, ipv6Address.Equals(IPAddress.IPv6Any));
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressFamilyNotSupported)
        {
            //IPv6 is disabled, e.g. in some Docker networks, so only IPv4 matters
            return true;
        }
    }

    private static bool CanListen(IPAddress address, int port, bool dualMode)
    {
        var listener = new TcpListener(address, port);
        if (dualMode)
        {
            listener.Server.DualMode = true;
        }
        try
        {
            listener.Start();
            return true;
        }
        catch (SocketException ex) when (ex.SocketErrorCode is SocketError.AddressAlreadyInUse or SocketError.AccessDenied)
        {
            return false;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static int? GetPort(string url)
    {
        var match = PortRegex().Match(url);
        return match.Success ? int.Parse(match.Groups[1].Value) : default;
    }

    [GeneratedRegex(@":(\d+)(/.*)?$")]
    private static partial Regex PortRegex();
}
