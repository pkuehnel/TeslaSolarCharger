using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using TeslaSolarCharger.Server.Services.Https.Contracts;
using TeslaSolarCharger.Shared.Helper;

namespace TeslaSolarCharger.Server.Services.Https;

public class LocalNetworkNameProvider(ILogger<LocalNetworkNameProvider> logger) : ILocalNetworkNameProvider
{
    public List<string> GetLocalHostNamesAndAddresses()
    {
        var names = new List<string> { "localhost", };
        try
        {
            names.Add(Dns.GetHostName());
        }
        catch (SocketException ex)
        {
            logger.LogWarning(ex, "Could not read the host name for the HTTPS certificate");
        }

        try
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up || n.NetworkInterfaceType == NetworkInterfaceType.Loopback);
            foreach (var networkInterface in networkInterfaces)
            {
                //Link-local IPv6 addresses only work together with an interface name, which no browser address can hold
                names.AddRange(networkInterface.GetIPProperties().UnicastAddresses
                    .Select(unicastAddress => unicastAddress.Address)
                    .Where(address => !address.IsIPv6LinkLocal)
                    .Select(address => address.ToString()));
            }
        }
        catch (NetworkInformationException ex)
        {
            logger.LogWarning(ex, "Could not read the network addresses for the HTTPS certificate");
        }

        return names.Select(HttpsHostNameHelper.Normalize)
            .Where(HttpsHostNameHelper.IsValidHostName)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
