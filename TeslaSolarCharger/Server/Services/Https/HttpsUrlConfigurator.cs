using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace TeslaSolarCharger.Server.Services.Https;

public enum HttpsUrlOutcome
{
    Added,
    AlreadyConfigured,
    Disabled,
    PortUnavailable,
}

/// <param name="Urls">The URLs to listen on, separated by semicolons.</param>
/// <param name="UsesDefaultUrls">Whether no URLs are configured, so TSC listens on its default ones.</param>
public record HttpsUrlDecision(HttpsUrlOutcome Outcome, string Urls, int Port, bool UsesDefaultUrls);

/// <summary>
/// Decides the URLs TSC listens on (ASPNETCORE_URLS or TSC's defaults) and whether it adds an HTTPS endpoint to them,
/// before the web host starts.
/// </summary>
public static partial class HttpsUrlConfigurator
{
    public const int DefaultHttpPort = 7190;
    //docker-compose files from before November 2025 forward a host port to port 80 of the container
    public const int LegacyHttpPort = 80;

    /// <param name="configuredUrls">The configured URLs, separated by semicolons.</param>
    /// <param name="httpsPort">The port to add HTTPS on, 0 or less disables it.</param>
    /// <param name="isPortAvailable">Whether nothing else listens on a port yet.</param>
    public static HttpsUrlDecision Decide(string? configuredUrls, int httpsPort, Func<int, bool> isPortAvailable)
    {
        var urls = (configuredUrls ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var usesDefaultUrls = urls.Length == 0;
        if (usesDefaultUrls)
        {
            urls = GetDefaultUrls(isPortAvailable);
        }
        HttpsUrlDecision Result(HttpsUrlOutcome outcome, IEnumerable<string> resultUrls) =>
            new(outcome, string.Join(';', resultUrls), httpsPort, usesDefaultUrls);

        if (httpsPort <= 0)
        {
            return Result(HttpsUrlOutcome.Disabled, urls);
        }
        if (urls.Any(url => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            return Result(HttpsUrlOutcome.AlreadyConfigured, urls);
        }
        //The URLs are not bound yet, so a port they use still looks available
        if (urls.Any(url => GetPort(url) == httpsPort) || !isPortAvailable(httpsPort))
        {
            return Result(HttpsUrlOutcome.PortUnavailable, urls);
        }
        return Result(HttpsUrlOutcome.Added, urls.Append($"https://+:{httpsPort}"));
    }

    /// <summary>
    /// Port 7190, plus port 80 for old docker-compose files as long as nothing else uses it, so a busy port 80 never
    /// keeps TSC from starting.
    /// </summary>
    private static string[] GetDefaultUrls(Func<int, bool> isPortAvailable) => isPortAvailable(LegacyHttpPort)
        ? [$"http://+:{DefaultHttpPort}", $"http://+:{LegacyHttpPort}",]
        : [$"http://+:{DefaultHttpPort}",];

    /// <summary>
    /// The decision is made before logging is set up, so it is logged afterwards.
    /// </summary>
    public static void LogDecision(ILogger logger, HttpsUrlDecision decision)
    {
        if (decision.UsesDefaultUrls)
        {
            logger.LogInformation("No URLs are configured in ASPNETCORE_URLS, so TSC listens on {urls}", decision.Urls);
        }
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
